using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var uid = GetUserId();
        var favorites = await _db.Favorites
            .Where(f => f.UserId == uid)
            .Include(f => f.Product)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.ProductId,
                f.CreatedAt,
                Product = new
                {
                    f.Product.Id,
                    f.Product.Name,
                    f.Product.Price,
                    f.Product.ImageUrl,
                    f.Product.Category,
                    f.Product.Brand,
                    f.Product.Stock,
                    f.Product.IsNew,
                    f.Product.IsFeatured,
                }
            })
            .ToListAsync();

        return Ok(favorites);
    }

    [HttpGet("ids")]
    public async Task<IActionResult> GetIds()
    {
        var uid = GetUserId();
        var ids = await _db.Favorites
            .Where(f => f.UserId == uid)
            .Select(f => f.ProductId)
            .ToListAsync();
        return Ok(ids);
    }

    [HttpPost("toggle/{productId}")]
    public async Task<IActionResult> Toggle(int productId)
    {
        var uid = GetUserId();
        var existing = await _db.Favorites
            .FirstOrDefaultAsync(f => f.UserId == uid && f.ProductId == productId);

        if (existing != null)
        {
            _db.Favorites.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { isFavorite = false });
        }

        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound();

        _db.Favorites.Add(new Favorite { UserId = uid, ProductId = productId });
        await _db.SaveChangesAsync();
        return Ok(new { isFavorite = true });
    }
}
