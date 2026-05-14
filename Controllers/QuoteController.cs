using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/quote")]
public class QuoteController : ControllerBase
{
    private readonly AppDbContext _db;
    public QuoteController(AppDbContext db) => _db = db;

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim) : null;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Phone))
            return BadRequest(new { message = "Ad, e-posta ve telefon zorunludur." });

        var req = new QuoteRequest
        {
            UserId = GetUserId(),
            FullName = dto.FullName.Trim(),
            Email = dto.Email.Trim().ToLower(),
            Phone = dto.Phone.Trim(),
            CompanyName = dto.CompanyName?.Trim() ?? string.Empty,
            City = dto.City.Trim(),
            ProjectType = dto.ProjectType,
            SystemSize = dto.SystemSize,
            Roof = dto.Roof,
            MonthlyBill = dto.MonthlyBill,
            Note = dto.Note?.Trim() ?? string.Empty,
        };

        _db.QuoteRequests.Add(req);

        var uid = GetUserId();
        if (uid.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = uid.Value,
                Title = "Teklif Talebiniz Alındı",
                Message = "Fiyat teklifiniz alınmıştır. Uzman ekibimiz en kısa sürede sizinle iletişime geçecektir.",
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { id = req.Id, message = "Teklif talebiniz başarıyla alındı. Ekibimiz sizinle iletişime geçecektir." });
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMy()
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var list = await _db.QuoteRequests
            .Where(r => r.UserId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.ProjectType, r.SystemSize, r.City, r.Status, r.AdminNote, r.CreatedAt })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var query = _db.QuoteRequests.AsQueryable();
        if (!string.IsNullOrEmpty(status)) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.FullName, r.Email, r.Phone, r.CompanyName, r.City,
                r.ProjectType, r.SystemSize, r.Roof, r.MonthlyBill, r.Note,
                r.Status, r.AdminNote, r.CreatedAt, r.UserId,
            })
            .ToListAsync();
        return Ok(new { total, items });
    }

    [HttpPut("admin/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] AdminUpdateQuoteDto dto)
    {
        var req = await _db.QuoteRequests.FindAsync(id);
        if (req == null) return NotFound();
        req.Status = dto.Status;
        req.AdminNote = dto.AdminNote;

        if (req.UserId.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = req.UserId.Value,
                Title = "Teklif Talebi Güncellendi",
                Message = $"Fiyat teklifiniz güncellendi." + (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Not: {dto.AdminNote}"),
                Type = "order",
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { req.Id, req.Status, req.AdminNote });
    }

    public record CreateQuoteDto(
        string FullName, string Email, string Phone,
        string? CompanyName, string City,
        string ProjectType, string SystemSize, string Roof,
        decimal? MonthlyBill, string? Note);

    public record AdminUpdateQuoteDto(string Status, string? AdminNote);
}
