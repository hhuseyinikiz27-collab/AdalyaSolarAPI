using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/newsletter")]
public class NewsletterController : ControllerBase
{
    private readonly AppDbContext _db;
    public NewsletterController(AppDbContext db) { _db = db; }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] NewsletterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
            return BadRequest(new { message = "Geçerli bir e-posta adresi girin." });

        var existing = await _db.NewsletterSubscriptions.FirstOrDefaultAsync(s => s.Email == dto.Email);
        if (existing != null)
        {
            if (existing.IsActive) return Ok(new { message = "Bu e-posta zaten abone." });
            existing.IsActive = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Aboneliğiniz yeniden aktifleştirildi!" });
        }

        _db.NewsletterSubscriptions.Add(new NewsletterSubscription { Email = dto.Email });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Kampanya bildirimlerine başarıyla abone oldunuz!" });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] NewsletterDto dto)
    {
        var sub = await _db.NewsletterSubscriptions.FirstOrDefaultAsync(s => s.Email == dto.Email);
        if (sub == null) return NotFound(new { message = "Bu e-posta abone değil." });
        sub.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Aboneliğiniz iptal edildi." });
    }
}

public record NewsletterDto(string Email);

[ApiController]
[Route("api/stock-notify")]
public class StockNotifyController : ControllerBase
{
    private readonly AppDbContext _db;
    public StockNotifyController(AppDbContext db) { _db = db; }

    [HttpPost]
    public async Task<IActionResult> Request([FromBody] StockNotifyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
            return BadRequest(new { message = "Geçerli bir e-posta girin." });

        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product == null) return NotFound();

        var existing = await _db.StockNotificationRequests
            .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.Email == dto.Email && !r.Notified);
        if (existing != null) return Ok(new { message = "Zaten kayıtlısınız, stok gelince haber vereceğiz!" });

        _db.StockNotificationRequests.Add(new StockNotificationRequest { ProductId = dto.ProductId, Email = dto.Email });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Stok geldiğinde e-posta ile bilgilendireceğiz!" });
    }
}

public record StockNotifyDto(int ProductId, string Email);
