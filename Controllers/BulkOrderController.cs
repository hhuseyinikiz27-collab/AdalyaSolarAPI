using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/bulk-order")]
public class BulkOrderController : ControllerBase
{
    private readonly AppDbContext _db;
    public BulkOrderController(AppDbContext db) => _db = db;

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : null;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BulkOrderCreateDto dto)
    {
        var userId = GetUserId();

        var req = new BulkOrderRequest
        {
            UserId = userId,
            CompanyName = dto.CompanyName.Trim(),
            ContactName = dto.ContactName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone.Trim(),
            City = dto.City.Trim(),
            ItemsJson = dto.ItemsJson,
            DeliveryAddress = dto.DeliveryAddress.Trim(),
            Note = dto.Note?.Trim() ?? string.Empty,
        };
        _db.BulkOrderRequests.Add(req);

        if (userId.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId.Value,
                Title = "Toplu Sipariş Talebiniz Alındı",
                Message = "Toplu sipariş talebiniz incelemeye alındı. En kısa sürede size dönüş yapacağız.",
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { id = req.Id, message = "Toplu sipariş talebiniz başarıyla alındı." });
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMy()
    {
        var uid = GetUserId()!.Value;
        var items = await _db.BulkOrderRequests
            .Where(r => r.UserId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.CompanyName, r.Status, r.AdminNote, r.CreatedAt })
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll([FromQuery] string? status, [FromQuery] int page = 1)
    {
        var q = _db.BulkOrderRequests.Include(r => r.User).AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(r => r.Status == status);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * 20).Take(20)
            .Select(r => new {
                r.Id, r.CompanyName, r.ContactName, r.Email, r.Phone, r.City,
                r.ItemsJson, r.DeliveryAddress, r.Note, r.Status, r.AdminNote, r.CreatedAt,
                UserName = r.User != null ? r.User.Name : null,
                UserEmail = r.User != null ? r.User.Email : null,
            })
            .ToListAsync();

        return Ok(new { total, items });
    }

    [HttpPut("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] BulkOrderUpdateDto dto)
    {
        var req = await _db.BulkOrderRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
        if (req == null) return NotFound();

        req.Status = dto.Status;
        if (dto.AdminNote != null) req.AdminNote = dto.AdminNote.Trim();

        if (req.UserId.HasValue)
        {
            var statusLabels = new Dictionary<string, string>
            {
                ["inceleniyor"] = "inceleniyor",
                ["teklif-gonderildi"] = "Teklif gönderildi",
                ["tamamlandi"] = "tamamlandı",
                ["reddedildi"] = "reddedildi",
            };
            var label = statusLabels.TryGetValue(dto.Status, out var l) ? l : dto.Status;
            _db.Notifications.Add(new Notification
            {
                UserId = req.UserId.Value,
                Title = "Toplu Sipariş Güncellendi",
                Message = $"Toplu sipariş talebinizin durumu '{label}' olarak güncellendi." +
                    (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Not: {dto.AdminNote}"),
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { req.Id, req.Status, req.AdminNote });
    }
}

public class BulkOrderCreateDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public record BulkOrderUpdateDto(string Status, string? AdminNote);
