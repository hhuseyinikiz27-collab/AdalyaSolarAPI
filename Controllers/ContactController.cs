using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Services;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public ContactController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "Ad, e-posta ve mesaj zorunludur." });

        var msg = new ContactMessage
        {
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim(),
            Phone = dto.Phone?.Trim() ?? "",
            Subject = dto.Subject?.Trim() ?? "",
            Message = dto.Message.Trim(),
        };

        _db.ContactMessages.Add(msg);
        await _db.SaveChangesAsync();

        // Admin e-postasına bildirim gönder
        var adminEmail = await _db.SiteSettings
            .Where(s => s.Key == "site.email")
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? "info@adalyasolar.com";

        var emailBody =
            $"Yeni iletişim mesajı alındı.\n\n" +
            $"Ad: {msg.Name}\n" +
            $"E-posta: {msg.Email}\n" +
            $"Telefon: {msg.Phone}\n" +
            $"Konu: {msg.Subject}\n\n" +
            $"Mesaj:\n{msg.Message}";

        _ = _email.SendAsync(adminEmail, "Adalya Solar", $"Yeni Mesaj: {msg.Subject}", emailBody);

        return Ok(new { message = "Mesajınız alındı. En kısa sürede size dönüş yapacağız." });
    }
}

public record ContactDto(string Name, string Email, string? Phone, string? Subject, string Message);
