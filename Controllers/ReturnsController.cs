using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize]
public class ReturnsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReturnsController(AppDbContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── User endpoints ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetMy()
    {
        var uid = GetUserId();
        var requests = await _db.ReturnRequests
            .Where(r => r.UserId == uid)
            .Include(r => r.Order)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.OrderId, r.Type, r.Reason,
                r.Status, r.AdminNote, r.CreatedAt, r.ResolvedAt,
                OrderTotal = r.Order.Total,
                OrderCreatedAt = r.Order.CreatedAt,
            })
            .ToListAsync();
        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReturnRequestDto dto)
    {
        var uid = GetUserId();

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == uid);
        if (order == null) return NotFound(new { message = "Sipariş bulunamadı." });

        // İade sadece teslim edilmiş siparişler için; iptal talebi diğerleri için
        if (dto.Type == "iade" && order.Status != "teslim-edildi")
            return BadRequest(new { message = "Yalnızca teslim edilmiş siparişler için iade talebi oluşturulabilir." });

        if (order.Status == "iptal")
            return BadRequest(new { message = "İptal edilmiş siparişler için talep oluşturulamaz." });

        // Aynı sipariş için bekleyen/onaylanan talep varsa engelle
        var existing = await _db.ReturnRequests.AnyAsync(r =>
            r.OrderId == dto.OrderId && (r.Status == "beklemede" || r.Status == "onaylandi"));
        if (existing)
            return BadRequest(new { message = "Bu sipariş için zaten aktif bir talep mevcut." });

        var req = new ReturnRequest
        {
            OrderId = dto.OrderId,
            UserId = uid,
            Type = dto.Type,
            Reason = dto.Reason,
        };
        _db.ReturnRequests.Add(req);
        await _db.SaveChangesAsync();

        return Ok(new { req.Id, req.Status, req.CreatedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = GetUserId();
        var req = await _db.ReturnRequests.FirstOrDefaultAsync(r => r.Id == id && r.UserId == uid);
        if (req == null) return NotFound();
        if (req.Status != "beklemede")
            return BadRequest(new { message = "Yalnızca beklemedeki talepler iptal edilebilir." });
        _db.ReturnRequests.Remove(req);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── Admin endpoints ───────────────────────────────────────────────────────

    [HttpGet("admin/pending-count")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> PendingCount()
    {
        var count = await _db.ReturnRequests.CountAsync(r => r.Status == "beklemede");
        return Ok(new { count });
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll([FromQuery] string? status)
    {
        var query = _db.ReturnRequests
            .Include(r => r.Order)
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.OrderId, r.Type, r.Reason,
                r.Status, r.AdminNote, r.CreatedAt, r.ResolvedAt,
                UserName = r.User.Name,
                UserEmail = r.User.Email,
                OrderTotal = r.Order.Total,
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPut("admin/{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] ReturnStatusDto dto)
    {
        var req = await _db.ReturnRequests.FindAsync(id);
        if (req == null) return NotFound();
        req.Status = dto.Status;
        req.AdminNote = dto.AdminNote;
        if (dto.Status is "onaylandi" or "reddedildi" or "tamamlandi")
            req.ResolvedAt = DateTime.UtcNow;

        // İptal talebi onaylandı/tamamlandıysa sipariş durumunu da güncelle
        if (req.Type == "iptal" && dto.Status is "onaylandi" or "tamamlandi")
        {
            var order = await _db.Orders.FindAsync(req.OrderId);
            if (order != null) order.Status = "iptal";
        }

        await _db.SaveChangesAsync();

        // Kullanıcıya bildirim gönder
        var statusLabel = dto.Status switch
        {
            "onaylandi"   => "Onaylandı",
            "reddedildi"  => "Reddedildi",
            "tamamlandi"  => "Tamamlandı",
            _ => dto.Status
        };
        _db.Notifications.Add(new Notification
        {
            UserId = req.UserId,
            Title = $"İade/İptal Talebiniz {statusLabel}",
            Message = string.IsNullOrEmpty(dto.AdminNote)
                ? $"#{req.OrderId} numaralı siparişiniz için talebiniz {statusLabel.ToLowerInvariant()}."
                : $"#{req.OrderId} numaralı talebiniz {statusLabel.ToLowerInvariant()}. Not: {dto.AdminNote}",
            Type = "order",
        });
        await _db.SaveChangesAsync();

        return Ok();
    }
}

public class ReturnRequestDto
{
    public int OrderId { get; set; }
    public string Type { get; set; } = "iade";
    public string Reason { get; set; } = string.Empty;
}

public class ReturnStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}
