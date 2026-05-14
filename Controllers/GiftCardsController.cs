using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/giftcards")]
public class GiftCardsController : ControllerBase
{
    private readonly AppDbContext _db;
    public GiftCardsController(AppDbContext db) => _db = db;

    // Kullanıcı kodu doğrulayıp bakiyeyi öğrenir
    [HttpGet("validate/{code}")]
    public async Task<IActionResult> Validate(string code)
    {
        var gc = await _db.GiftCards.FirstOrDefaultAsync(g =>
            g.Code == code.Trim().ToUpper() && g.IsActive && g.Balance > 0);
        if (gc == null) return BadRequest(new { message = "Geçersiz veya bakiyesi tükenmiş hediye kartı." });
        if (gc.ExpiresAt.HasValue && gc.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Hediye kartının süresi dolmuş." });
        return Ok(new { balance = gc.Balance, amount = gc.Amount });
    }

    // Checkout sırasında bakiyeyi düş (OrdersController'dan çağrılacak ama kullanıcı sepet sayfasından da tetikleyebilir)
    [HttpPost("use")]
    [Authorize]
    public async Task<IActionResult> Use([FromBody] UseGiftCardDto dto)
    {
        var gc = await _db.GiftCards.FirstOrDefaultAsync(g =>
            g.Code == dto.Code.Trim().ToUpper() && g.IsActive && g.Balance > 0);
        if (gc == null) return BadRequest(new { message = "Geçersiz hediye kartı." });
        if (gc.ExpiresAt.HasValue && gc.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Hediye kartının süresi dolmuş." });

        decimal applied = Math.Min(gc.Balance, dto.OrderTotal);
        gc.Balance -= applied;
        if (gc.Balance <= 0) gc.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { applied, remainingBalance = gc.Balance });
    }

    // ── Admin endpoints ───────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.GiftCards.OrderByDescending(g => g.CreatedAt).ToListAsync());

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] CreateGiftCardDto dto)
    {
        var code = dto.Code?.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
            code = GenerateCode();

        if (await _db.GiftCards.AnyAsync(g => g.Code == code))
            return BadRequest(new { message = "Bu kod zaten kullanılıyor." });

        var gc = new GiftCard
        {
            Code = code,
            Amount = dto.Amount,
            Balance = dto.Amount,
            IsActive = true,
            ExpiresAt = dto.ExpiresAt,
            Note = dto.Note ?? string.Empty,
        };
        _db.GiftCards.Add(gc);
        await _db.SaveChangesAsync();
        return Ok(gc);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var gc = await _db.GiftCards.FindAsync(id);
        if (gc == null) return NotFound();
        _db.GiftCards.Remove(gc);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return string.Concat(Enumerable.Range(0, 3).Select(_ =>
            new string(Enumerable.Range(0, 4).Select(__ => chars[rng.Next(chars.Length)]).ToArray())
        ).ToArray().Select((s, i) => i == 0 ? s : "-" + s));
    }
}

public record UseGiftCardDto(string Code, decimal OrderTotal);
public record CreateGiftCardDto(decimal Amount, string? Code = null, DateTime? ExpiresAt = null, string? Note = null);
