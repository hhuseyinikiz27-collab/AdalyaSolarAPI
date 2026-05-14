using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/warranty")]
[Authorize]
public class WarrantyController : ControllerBase
{
    private readonly AppDbContext _db;
    public WarrantyController(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var uid = GetUserId();
        var list = await _db.WarrantyRegistrations
            .Where(w => w.UserId == uid)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                w.OrderId,
                w.ProductId,
                w.ProductName,
                w.SerialNumber,
                w.WarrantyMonths,
                w.PurchaseDate,
                w.ExpiresAt,
                w.Status,
                w.CreatedAt,
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterWarrantyDto dto)
    {
        var uid = GetUserId();

        // Validate the order belongs to the user and is delivered
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == uid);

        if (order == null) return NotFound(new { message = "Sipariş bulunamadı." });
        if (order.Status != "teslim-edildi")
            return BadRequest(new { message = "Garanti kaydı yalnızca teslim edilen siparişler için yapılabilir." });

        var item = order.Items.FirstOrDefault(i => i.ProductId == dto.ProductId);
        if (item == null) return NotFound(new { message = "Sipariş ürünü bulunamadı." });

        // Check duplicate
        var exists = await _db.WarrantyRegistrations
            .AnyAsync(w => w.UserId == uid && w.OrderId == dto.OrderId && w.ProductId == dto.ProductId);
        if (exists) return Conflict(new { message = "Bu ürün için zaten garanti kaydı yapılmış." });

        var product = await _db.Products.FindAsync(dto.ProductId);
        int warrantyMonths = product?.WarrantyMonths > 0 ? product.WarrantyMonths : 24;

        var purchaseDate = order.CreatedAt.ToUniversalTime();
        var expiresAt = purchaseDate.AddMonths(warrantyMonths);

        var reg = new WarrantyRegistration
        {
            UserId = uid,
            OrderId = dto.OrderId,
            ProductId = dto.ProductId,
            ProductName = item.Product?.Name ?? "Ürün",
            SerialNumber = dto.SerialNumber.Trim(),
            WarrantyMonths = warrantyMonths,
            PurchaseDate = purchaseDate,
            ExpiresAt = expiresAt,
            Status = expiresAt > DateTime.UtcNow ? "active" : "expired",
        };

        _db.WarrantyRegistrations.Add(reg);

        _db.Notifications.Add(new Notification
        {
            UserId = uid,
            Title = "Garanti Kaydı Tamamlandı",
            Message = $"{item.Product?.Name ?? "Ürününüzün"} garantisi {expiresAt:dd/MM/yyyy} tarihine kadar geçerlidir.",
            Type = "order",
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            reg.Id,
            reg.ProductName,
            reg.SerialNumber,
            reg.WarrantyMonths,
            reg.PurchaseDate,
            reg.ExpiresAt,
            reg.Status,
        });
    }

    // Admin: list all warranty registrations
    [HttpGet("admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _db.WarrantyRegistrations
            .Include(w => w.User)
            .OrderByDescending(w => w.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new
            {
                w.Id,
                w.OrderId,
                w.ProductId,
                w.ProductName,
                w.SerialNumber,
                w.WarrantyMonths,
                w.PurchaseDate,
                w.ExpiresAt,
                w.Status,
                w.CreatedAt,
                UserName = w.User.Name,
                UserEmail = w.User.Email,
            })
            .ToListAsync();

        return Ok(new { total, items });
    }

    public record RegisterWarrantyDto(int OrderId, int ProductId, string SerialNumber);
}
