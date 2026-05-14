using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/abandoned-cart")]
[Authorize]
public class AbandonedCartsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AbandonedCartsController(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public record TrackDto(List<CartItemDto> Items, decimal Total);
    public record CartItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice, string? ImageUrl);

    [HttpPost("track")]
    public async Task<IActionResult> Track([FromBody] TrackDto dto)
    {
        if (dto.Items.Count == 0) return Ok();
        var uid = GetUserId();
        var existing = await _db.AbandonedCarts.FirstOrDefaultAsync(c => c.UserId == uid);
        if (existing == null)
        {
            _db.AbandonedCarts.Add(new AbandonedCart
            {
                UserId = uid,
                ItemsJson = JsonSerializer.Serialize(dto.Items),
                TotalAmount = dto.Total,
            });
        }
        else
        {
            existing.ItemsJson = JsonSerializer.Serialize(dto.Items);
            existing.TotalAmount = dto.Total;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.EmailSentAt = null; // reset so a new email can be sent
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        var uid = GetUserId();
        var existing = await _db.AbandonedCarts.FirstOrDefaultAsync(c => c.UserId == uid);
        if (existing != null) _db.AbandonedCarts.Remove(existing);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
