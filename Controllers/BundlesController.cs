using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/bundles")]
public class BundlesController : ControllerBase
{
    private readonly AppDbContext _db;
    public BundlesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bundles = await _db.Bundles
            .Where(b => b.IsActive)
            .Include(b => b.Items).ThenInclude(i => i.Product)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Name, b.Description, b.BundlePrice, b.IsActive, b.CreatedAt,
                OriginalPrice = b.Items.Sum(i => i.Product.Price * i.Quantity),
                Items = b.Items.Select(i => new
                {
                    i.Id, i.ProductId, i.Quantity,
                    ProductName = i.Product.Name,
                    ProductImageUrl = i.Product.ImageUrl,
                    ProductPrice = i.Product.Price,
                }),
            })
            .ToListAsync();
        return Ok(bundles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var bundle = await _db.Bundles
            .Include(b => b.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bundle == null) return NotFound();
        return Ok(new
        {
            bundle.Id, bundle.Name, bundle.Description, bundle.BundlePrice, bundle.IsActive,
            OriginalPrice = bundle.Items.Sum(i => i.Product.Price * i.Quantity),
            Items = bundle.Items.Select(i => new
            {
                i.Id, i.ProductId, i.Quantity,
                ProductName = i.Product.Name,
                ProductImageUrl = i.Product.ImageUrl,
                ProductPrice = i.Product.Price,
            }),
        });
    }

    // Admin endpoints
    [HttpGet("admin-all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllAdmin()
    {
        var bundles = await _db.Bundles
            .Include(b => b.Items).ThenInclude(i => i.Product)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id, b.Name, b.Description, b.BundlePrice, b.IsActive, b.CreatedAt,
                OriginalPrice = b.Items.Sum(i => i.Product.Price * i.Quantity),
                Items = b.Items.Select(i => new
                {
                    i.Id, i.ProductId, i.Quantity,
                    ProductName = i.Product.Name,
                    ProductImageUrl = i.Product.ImageUrl,
                    ProductPrice = i.Product.Price,
                }),
            })
            .ToListAsync();
        return Ok(bundles);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create([FromBody] BundleUpsertDto dto)
    {
        var bundle = new Bundle
        {
            Name = dto.Name,
            Description = dto.Description,
            BundlePrice = dto.BundlePrice,
            IsActive = dto.IsActive,
            Items = dto.Items.Select(i => new BundleItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
            }).ToList(),
        };
        _db.Bundles.Add(bundle);
        await _db.SaveChangesAsync();
        return Ok(new { bundle.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] BundleUpsertDto dto)
    {
        var bundle = await _db.Bundles.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id);
        if (bundle == null) return NotFound();

        bundle.Name = dto.Name;
        bundle.Description = dto.Description;
        bundle.BundlePrice = dto.BundlePrice;
        bundle.IsActive = dto.IsActive;

        _db.BundleItems.RemoveRange(bundle.Items);
        bundle.Items = dto.Items.Select(i => new BundleItem
        {
            BundleId = id,
            ProductId = i.ProductId,
            Quantity = i.Quantity,
        }).ToList();

        await _db.SaveChangesAsync();
        return Ok(new { id });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var bundle = await _db.Bundles.FindAsync(id);
        if (bundle == null) return NotFound();
        _db.Bundles.Remove(bundle);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record BundleItemDto(int ProductId, int Quantity);
public record BundleUpsertDto(string Name, string Description, decimal BundlePrice, bool IsActive, List<BundleItemDto> Items);
