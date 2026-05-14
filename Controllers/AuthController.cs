using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.DTOs;
using AdalyaSolarAPI.Models;
using AdalyaSolarAPI.Services;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokenService;
    private readonly IWebHostEnvironment _env;
    private readonly EmailService _email;

    public AuthController(AppDbContext db, TokenService tokenService, IWebHostEnvironment env, EmailService email)
    {
        _db = db;
        _tokenService = tokenService;
        _env = env;
        _email = email;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            return BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = normalizedEmail,
            Phone = dto.Phone?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            ReferralCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Referral kodu varsa referans veren kullanıcıya 100 puan, yeni kullanıcıya 50 puan
        if (!string.IsNullOrWhiteSpace(dto.ReferralCode))
        {
            var referrer = await _db.Users.FirstOrDefaultAsync(u => u.ReferralCode == dto.ReferralCode);
            if (referrer != null && referrer.Id != user.Id)
            {
                referrer.LoyaltyPoints += 100;
                referrer.ReferralCount++;
                _db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    UserId = referrer.Id,
                    Points = 100,
                    Type = "earned",
                    Description = $"{user.Name} davet edildi (+100 puan)",
                });
                _db.Notifications.Add(new Notification
                {
                    UserId = referrer.Id,
                    Title = "Davet Bonusu!",
                    Message = $"{user.Name} davet linkinizle kayıt oldu. 100 puan kazandınız!",
                    Type = "promo",
                });

                user.LoyaltyPoints += 50;
                _db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    UserId = user.Id,
                    Points = 50,
                    Type = "earned",
                    Description = "Davet ile kayıt bonusu (+50 puan)",
                });
                await _db.SaveChangesAsync();
            }
        }

        return Ok(new { token = _tokenService.CreateToken(user), user.Id, user.Name, user.Email, user.Role });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "E-posta veya şifre hatalı." });

        // Hesap kilitli mi?
        if (user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
            return Unauthorized(new { message = $"Hesabınız geçici olarak kilitlendi. {remaining} dakika sonra tekrar deneyin. Sebep: {user.LockoutReason}" });
        }

        // Kilidi geçtiyse temizle
        if (user.LockoutUntil.HasValue && user.LockoutUntil <= DateTime.UtcNow)
        {
            user.LockoutUntil = null;
            user.LockoutReason = null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        _db.UserSecurityLogs.Add(new UserSecurityLog
        {
            UserId = user.Id,
            Action = "login",
            Details = $"Giriş yapıldı."
        });
        await _db.SaveChangesAsync();

        return Ok(new { token = _tokenService.CreateToken(user), user.Id, user.Name, user.Email, user.Role });
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.Name = dto.Name;
        user.Phone = dto.Phone;
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.PhotoUrl, user.Role });
    }

    [HttpPost("profile/photo")]
    [Authorize]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            return BadRequest(new { message = "Sadece JPG, PNG veya WebP yükleyebilirsiniz." });

        var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "avatars");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"user_{userId}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        user.PhotoUrl = $"/uploads/avatars/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { photoUrl = user.PhotoUrl });
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Mevcut şifreniz yanlış." });

        if (BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "Yeni şifreniz mevcut şifrenizle aynı olamaz." });

        if (dto.NewPassword.Length < 6)
            return BadRequest(new { message = "Yeni şifre en az 6 karakter olmalıdır." });

        // Güvenlik ayarlarını oku
        var settings = await _db.SiteSettings
            .Where(s => s.Key.StartsWith("security."))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        int lockoutMinutes = int.TryParse(settings.GetValueOrDefault("security.passwordLockoutMinutes", "30"), out var lm) ? lm : 30;
        int windowMinutes = int.TryParse(settings.GetValueOrDefault("security.rapidChangeWindowMinutes", "60"), out var wm) ? wm : 60;
        int changeLimit = int.TryParse(settings.GetValueOrDefault("security.rapidChangeLimit", "2"), out var cl) ? cl : 2;

        // Son windowMinutes dakikada kaç kez şifre değiştirildi?
        var windowStart = DateTime.UtcNow.AddMinutes(-windowMinutes);
        var recentChanges = await _db.UserSecurityLogs
            .CountAsync(l => l.UserId == userId && l.Action == "password_changed" && l.CreatedAt >= windowStart);

        var now = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordChangedAt = now;

        _db.UserSecurityLogs.Add(new UserSecurityLog
        {
            UserId = userId,
            Action = "password_changed",
            Details = $"Şifre değiştirildi."
        });

        // Hız sınırı aşıldıysa kilitle
        if (recentChanges >= changeLimit)
        {
            user.LockoutUntil = now.AddMinutes(lockoutMinutes);
            user.LockoutReason = $"Kısa sürede çok fazla şifre değişikliği ({recentChanges + 1} kez {windowMinutes} dk içinde).";

            _db.UserSecurityLogs.Add(new UserSecurityLog
            {
                UserId = userId,
                Action = "locked_out",
                Details = $"Hesap {lockoutMinutes} dakika kilitlendi. Sebep: {user.LockoutReason}"
            });

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Hesabınız Geçici Olarak Kilitlendi",
                Message = $"Kısa sürede çok fazla şifre değişikliği tespit edildi. Hesabınız {lockoutMinutes} dakika süreyle kilitlendi.",
                Type = "security",
            });

            await _db.SaveChangesAsync();

            // E-posta bildirimi (fire & forget)
            _ = _email.SendAsync(
                user.Email, user.Name,
                "⚠️ Hesabınız Geçici Olarak Kilitlendi - Adalya Solar",
                $"Merhaba {user.Name},\n\n" +
                $"Kısa sürede birden fazla şifre değişikliği yapıldığı tespit edildi.\n" +
                $"Güvenliğiniz için hesabınız {lockoutMinutes} dakika süreyle kilitlenmiştir.\n\n" +
                $"Tarih/Saat: {now.AddHours(3):dd.MM.yyyy HH:mm} (Türkiye saati)\n\n" +
                $"Bu işlemi siz yapmadıysanız lütfen bizimle iletişime geçin.\n\n" +
                $"Adalya Solar Enerji"
            );

            return Ok(new { message = "Şifreniz güncellendi. Ancak çok fazla şifre değişikliği nedeniyle hesabınız geçici olarak kilitlendi.", locked = true, lockoutMinutes });
        }

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = "Şifreniz Değiştirildi",
            Message = $"Hesabınızın şifresi {now.AddHours(3):dd.MM.yyyy HH:mm} tarihinde başarıyla güncellendi. Bu işlemi siz yapmadıysanız lütfen bizimle iletişime geçin.",
            Type = "security",
        });

        await _db.SaveChangesAsync();

        // Başarılı şifre değişikliği e-postası (fire & forget)
        _ = _email.SendAsync(
            user.Email, user.Name,
            "🔐 Şifreniz Değiştirildi - Adalya Solar",
            $"Merhaba {user.Name},\n\n" +
            $"Hesabınızın şifresi başarıyla değiştirildi.\n\n" +
            $"Tarih/Saat: {now.AddHours(3):dd.MM.yyyy HH:mm} (Türkiye saati)\n\n" +
            $"Bu işlemi siz yapmadıysanız lütfen hemen şifrenizi sıfırlayın ve bizimle iletişime geçin.\n\n" +
            $"Adalya Solar Enerji"
        );

        return Ok(new { message = "Şifreniz başarıyla güncellendi." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLower());
        if (user != null)
        {
            user.PasswordResetToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
            await _db.SaveChangesAsync();

            var frontendUrl = _env.IsProduction()
                ? "https://adalyasolar.com"
                : "http://localhost:3000";
            var resetUrl = $"{frontendUrl}/sifre-sifirla?token={user.PasswordResetToken}";

            _ = _email.SendAsync(
                user.Email, user.Name,
                "Şifre Sıfırlama — Adalya Solar",
                $"Merhaba {user.Name},\n\nŞifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın (30 dakika geçerli):\n\n{resetUrl}\n\nBu isteği siz yapmadıysanız bu mesajı dikkate almayın.\n\nAdalya Solar Enerji"
            );
        }
        // Güvenlik gereği her durumda aynı mesajı dön (hesap varlığını gizle)
        return Ok(new { message = "E-posta adresinize kayıtlı hesap varsa sıfırlama bağlantısı gönderildi." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == dto.Token &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            return BadRequest(new { message = "Geçersiz veya süresi dolmuş sıfırlama bağlantısı." });

        if (dto.NewPassword.Length < 6)
            return BadRequest(new { message = "Şifre en az 6 karakter olmalıdır." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.PasswordChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Şifreniz başarıyla güncellendi. Giriş yapabilirsiniz." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();
        return Ok(new { user.Id, user.Name, user.Email, user.Phone, user.PhotoUrl, user.Role });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
    {
        // Verify token with Google
        using var http = new HttpClient();
        var response = await http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={dto.IdToken}");
        if (!response.IsSuccessStatusCode)
            return Unauthorized(new { message = "Geçersiz Google token." });

        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(json);

        if (!payload.TryGetProperty("email", out var emailProp) ||
            !payload.TryGetProperty("sub", out var subProp))
            return Unauthorized(new { message = "Google bilgileri alınamadı." });

        var email = emailProp.GetString()!;
        var googleId = subProp.GetString()!;
        var name = payload.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? email : email;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            user = new User
            {
                Name = name,
                Email = email,
                GoogleId = googleId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (user.GoogleId == null)
        {
            user.GoogleId = googleId;
            await _db.SaveChangesAsync();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { token = _tokenService.CreateToken(user), user.Id, user.Name, user.Email, user.Role });
    }

    [HttpGet("flash-notify")]
    [Authorize]
    public async Task<IActionResult> GetFlashNotify()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();
        return Ok(new { enabled = user.FlashSaleNotify });
    }

    [HttpPut("flash-notify")]
    [Authorize]
    public async Task<IActionResult> SetFlashNotify([FromBody] FlashNotifyDto dto)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();
        user.FlashSaleNotify = dto.Enabled;
        await _db.SaveChangesAsync();
        return Ok(new { enabled = user.FlashSaleNotify });
    }
}

public record FlashNotifyDto(bool Enabled);
