using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/loyalty")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly AppDbContext _db;
    public LoyaltyController(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetBalance()
    {
        var uid = GetUserId();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();

        var transactions = await _db.LoyaltyTransactions
            .Where(t => t.UserId == uid)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .Select(t => new { t.Id, t.Points, t.Type, t.Description, t.CreatedAt })
            .ToListAsync();

        return Ok(new { points = user.LoyaltyPoints, transactions });
    }

    // Called at checkout to lock in redemption; returns TL discount
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemDto dto)
    {
        var uid = GetUserId();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();

        if (dto.Points <= 0 || dto.Points > user.LoyaltyPoints)
            return BadRequest(new { message = "Geçersiz puan miktarı." });

        // 100 puan = 10 TL indirim
        decimal discount = Math.Floor(dto.Points / 100m) * 10;
        if (discount <= 0)
            return BadRequest(new { message = "En az 100 puan gerekli (= 10 ₺ indirim)." });

        // Deduct points temporarily (will be finalized on order create)
        user.LoyaltyPoints -= dto.Points;
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId = uid,
            Points = -dto.Points,
            Type = "redeemed",
            Description = $"{dto.Points} puan kullanıldı ({discount:N0} ₺ indirim)",
        });
        await _db.SaveChangesAsync();

        return Ok(new { discount, remainingPoints = user.LoyaltyPoints });
    }

    // Cancel a pending redemption (user removes it from checkout)
    [HttpDelete("redeem/cancel")]
    public async Task<IActionResult> CancelRedeem([FromBody] CancelRedeemDto dto)
    {
        var uid = GetUserId();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();

        user.LoyaltyPoints += dto.Points;
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId = uid,
            Points = dto.Points,
            Type = "earned",
            Description = "Kullanılan puan iade edildi",
        });
        await _db.SaveChangesAsync();

        return Ok();
    }

    // Public leaderboard — top 50 by points (masked name for privacy)
    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> Leaderboard()
    {
        var raw = await _db.Users
            .Where(u => u.Role != "admin" && u.LoyaltyPoints > 0)
            .OrderByDescending(u => u.LoyaltyPoints)
            .Take(50)
            .Select(u => new { u.Name, points = u.LoyaltyPoints, referralCount = u.ReferralCount })
            .ToListAsync();

        var top = raw.Select(u => new
        {
            name = u.Name.Length > 2
                ? u.Name[0] + new string('*', u.Name.Length - 2) + u.Name[^1]
                : u.Name,
            u.points,
            u.referralCount,
        }).ToList();

        return Ok(top);
    }

    // My rank
    [HttpGet("leaderboard/my-rank")]
    public async Task<IActionResult> MyRank()
    {
        var uid = GetUserId();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();

        var rank = await _db.Users
            .Where(u => u.Role != "admin" && u.LoyaltyPoints > user.LoyaltyPoints)
            .CountAsync() + 1;

        return Ok(new { rank, points = user.LoyaltyPoints });
    }

    // Transfer points to another user by email
    [HttpPost("gift")]
    public async Task<IActionResult> Gift([FromBody] GiftPointsDto dto)
    {
        var uid = GetUserId();
        if (dto.Points <= 0) return BadRequest(new { message = "Geçersiz puan miktarı." });
        if (dto.Points < 50) return BadRequest(new { message = "En az 50 puan hediye edebilirsiniz." });

        var sender = await _db.Users.FindAsync(uid);
        if (sender == null) return NotFound();
        if (sender.LoyaltyPoints < dto.Points)
            return BadRequest(new { message = "Yeterli puanınız yok." });

        var receiver = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.ReceiverEmail.Trim().ToLower() && u.Role != "admin");
        if (receiver == null) return NotFound(new { message = "Bu e-posta adresine kayıtlı kullanıcı bulunamadı." });
        if (receiver.Id == uid) return BadRequest(new { message = "Kendinize puan gönderemezsiniz." });

        sender.LoyaltyPoints -= dto.Points;
        receiver.LoyaltyPoints += dto.Points;

        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId = uid,
            Points = -dto.Points,
            Type = "redeemed",
            Description = $"{receiver.Name} kullanıcısına {dto.Points} puan hediye edildi",
        });
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId = receiver.Id,
            Points = dto.Points,
            Type = "earned",
            Description = $"{sender.Name} tarafından {dto.Points} puan hediye edildi",
        });
        _db.Notifications.Add(new Notification
        {
            UserId = receiver.Id,
            Title = "Puan Hediyesi!",
            Message = $"{sender.Name} size {dto.Points} sadakat puanı hediye etti! Toplam puanınız: {receiver.LoyaltyPoints}",
            Type = "promo",
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{receiver.Name} kullanıcısına {dto.Points} puan başarıyla gönderildi.", remainingPoints = sender.LoyaltyPoints });
    }

    public record RedeemDto(int Points);
    public record CancelRedeemDto(int Points);
    public record GiftPointsDto(string ReceiverEmail, int Points);
}
