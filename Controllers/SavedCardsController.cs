using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/cards")]
[Authorize]
public class SavedCardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SavedCardsController(AppDbContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var uid = GetUserId();
        return Ok(await _db.SavedCards
            .Where(c => c.UserId == uid)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavedCardDto dto)
    {
        var uid = GetUserId();

        // Kart numarasının son 4 hanesi dışındaki bilgileri almıyoruz
        if (dto.Last4.Length != 4 || !dto.Last4.All(char.IsDigit))
            return BadRequest(new { message = "Geçersiz kart bilgisi." });

        if (dto.IsDefault)
            await _db.SavedCards.Where(c => c.UserId == uid)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false));

        var card = new SavedCard
        {
            UserId = uid,
            CardHolderName = dto.CardHolderName.ToUpper(),
            Last4 = dto.Last4,
            ExpiryMonth = dto.ExpiryMonth,
            ExpiryYear = dto.ExpiryYear,
            CardType = dto.CardType,
            IsDefault = dto.IsDefault,
        };

        _db.SavedCards.Add(card);
        await _db.SaveChangesAsync();
        return Ok(card);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = GetUserId();
        var card = await _db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
        if (card == null) return NotFound();
        _db.SavedCards.Remove(card);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var uid = GetUserId();
        var card = await _db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
        if (card == null) return NotFound();

        await _db.SavedCards.Where(c => c.UserId == uid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false));

        card.IsDefault = true;
        await _db.SaveChangesAsync();
        return Ok(card);
    }
}

public class SavedCardDto
{
    public string CardHolderName { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string CardType { get; set; } = "Visa";
    public bool IsDefault { get; set; }
}
