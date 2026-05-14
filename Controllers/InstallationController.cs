using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/installation")]
public class InstallationController : ControllerBase
{
    private readonly AppDbContext _db;
    public InstallationController(AppDbContext db) => _db = db;

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : null;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest(new { message = "Ad, e-posta ve telefon zorunludur." });

        var req = new InstallationRequest
        {
            UserId = GetUserId(),
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone.Trim(),
            Address = dto.Address.Trim(),
            City = dto.City.Trim(),
            InstallationType = dto.InstallationType,
            SystemSize = dto.SystemSize,
            Note = dto.Note?.Trim() ?? string.Empty,
        };

        _db.InstallationRequests.Add(req);

        var uid = GetUserId();
        if (uid.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = uid.Value,
                Title = "Kurulum Talebiniz Alındı",
                Message = "Kurulum hizmeti talebiniz alınmıştır. Ekibimiz en kısa sürede sizinle iletişime geçecektir.",
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { id = req.Id, message = "Kurulum talebiniz başarıyla alındı. Ekibimiz sizinle iletişime geçecektir." });
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMy()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var list = await _db.InstallationRequests
            .Where(r => r.UserId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.InstallationType, r.SystemSize, r.City, r.Status, r.AdminNote, r.CreatedAt })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var query = _db.InstallationRequests.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.FullName, r.Email, r.Phone, r.Address, r.City,
                r.InstallationType, r.SystemSize, r.Note, r.Status, r.AdminNote, r.CreatedAt,
                r.UserId,
            })
            .ToListAsync();
        return Ok(new { total, items });
    }

    [HttpPut("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] AdminUpdateInstallationDto dto)
    {
        var req = await _db.InstallationRequests.FindAsync(id);
        if (req == null) return NotFound();
        req.Status = dto.Status;
        req.AdminNote = dto.AdminNote;

        if (req.UserId.HasValue)
        {
            var statusLabel = dto.Status switch
            {
                "inceleniyor" => "inceleniyor",
                "tamamlandi" => "tamamlandı",
                "reddedildi" => "reddedildi",
                _ => dto.Status,
            };
            _db.Notifications.Add(new Notification
            {
                UserId = req.UserId.Value,
                Title = "Kurulum Talebi Güncellendi",
                Message = $"Kurulum talebiniz '{statusLabel}' olarak güncellendi." + (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Not: {dto.AdminNote}"),
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { req.Id, req.Status, req.AdminNote });
    }

    public record CreateInstallationDto(
        string FullName, string Email, string Phone,
        string Address, string City,
        string InstallationType, string SystemSize,
        string? Note);

    public record AdminUpdateInstallationDto(string Status, string? AdminNote);
}
