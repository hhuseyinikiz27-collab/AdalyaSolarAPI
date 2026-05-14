using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category, [FromQuery] string? search)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search}%") ||
                                     EF.Functions.ILike(p.Description, $"%{search}%"));

        var result = await query.Select(p => new
        {
            p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.ImageUrl,
            p.Category, p.Brand, p.Stock, p.IsFeatured, p.IsNew, p.CreatedAt,
            p.VolumeDiscountsJson, p.FlashSalePrice, p.FlashSaleEndsAt, p.WarrantyMonths,
            FavoriteCount = _db.Favorites.Count(f => f.ProductId == p.Id),
            ReviewCount = _db.Reviews.Count(r => r.ProductId == p.Id),
            AvgRating = _db.Reviews.Where(r => r.ProductId == p.Id).Any()
                ? (double?)_db.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double)r.Rating)
                : null,
            Images = _db.ProductImages
                .Where(i => i.ProductId == p.Id)
                .OrderBy(i => i.SortOrder)
                .Select(i => new { i.Id, Url = i.Url, i.SortOrder })
                .ToList(),
        }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _db.Products
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.ImageUrl,
                p.Category, p.Brand, p.Stock, p.IsFeatured, p.IsNew, p.CreatedAt,
                FavoriteCount = _db.Favorites.Count(f => f.ProductId == p.Id),
                ReviewCount = _db.Reviews.Count(r => r.ProductId == p.Id),
                AvgRating = _db.Reviews.Where(r => r.ProductId == p.Id).Any()
                    ? (double?)_db.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double)r.Rating)
                    : null,
                Images = _db.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new { i.Id, Url = i.Url, i.SortOrder })
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        if (p == null) return NotFound();
        return Ok(p);
    }

    [HttpGet("{id}/documents")]
    public async Task<IActionResult> GetDocuments(int id)
    {
        var docs = await _db.ProductDocuments
            .Where(d => d.ProductId == id)
            .OrderBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.Name, d.FileUrl, d.FileType, d.SizeBytes })
            .ToListAsync();
        return Ok(docs);
    }

    [HttpGet("{id}/order-count")]
    public async Task<IActionResult> GetOrderCount(int id)
    {
        var from = DateTime.UtcNow.AddDays(-30);
        var count = await _db.OrderItems
            .CountAsync(i => i.ProductId == id && i.Order.CreatedAt >= from && i.Order.Status != "iptal");
        return Ok(new { count });
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured()
    {
        var result = await _db.Products
            .Where(p => p.IsFeatured)
            .Take(6)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.ImageUrl,
                p.Category, p.Brand, p.Stock, p.IsFeatured, p.IsNew, p.CreatedAt,
                FavoriteCount = _db.Favorites.Count(f => f.ProductId == p.Id),
                ReviewCount = _db.Reviews.Count(r => r.ProductId == p.Id),
                AvgRating = _db.Reviews.Where(r => r.ProductId == p.Id).Any()
                    ? (double?)_db.Reviews.Where(r => r.ProductId == p.Id).Average(r => (double)r.Rating)
                    : null,
                Images = _db.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new { i.Id, Url = i.Url, i.SortOrder })
                    .ToList(),
            })
            .ToListAsync();

        return Ok(result);
    }
}
