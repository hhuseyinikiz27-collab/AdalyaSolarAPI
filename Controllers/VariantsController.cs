using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api")]
public class VariantsController : ControllerBase
{
    private readonly AppDbContext _db;
    public VariantsController(AppDbContext db) => _db = db;

    // ── Public: get variants for a product ───────────────────────────────────

    [HttpGet("products/{productId}/variants")]
    public async Task<IActionResult> GetByProduct(int productId)
    {
        var variants = await _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.GroupName)
            .ThenBy(v => v.SortOrder)
            .Select(v => new
            {
                v.Id, v.ProductId, v.GroupName, v.Value,
                v.PriceAdjustment, v.Stock, v.IsDefault, v.SortOrder,
            })
            .ToListAsync();
        return Ok(variants);
    }

    // ── Admin CRUD ────────────────────────────────────────────────────────────

    [HttpPost("admin/products/{productId}/variants")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(int productId, [FromBody] VariantDto dto)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var variant = new ProductVariant
        {
            ProductId = productId,
            GroupName = dto.GroupName.Trim(),
            Value = dto.Value.Trim(),
            PriceAdjustment = dto.PriceAdjustment,
            Stock = dto.Stock,
            IsDefault = dto.IsDefault,
            SortOrder = dto.SortOrder,
        };

        // Yeni varsayılan seçilirse eskisini kaldır
        if (dto.IsDefault)
        {
            var existing = await _db.ProductVariants
                .Where(v => v.ProductId == productId && v.GroupName == dto.GroupName && v.IsDefault)
                .ToListAsync();
            existing.ForEach(v => v.IsDefault = false);
        }

        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync();
        return Ok(variant);
    }

    [HttpPut("admin/variants/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] VariantDto dto)
    {
        var variant = await _db.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();

        if (dto.IsDefault && !variant.IsDefault)
        {
            var existing = await _db.ProductVariants
                .Where(v => v.ProductId == variant.ProductId && v.GroupName == dto.GroupName && v.IsDefault && v.Id != id)
                .ToListAsync();
            existing.ForEach(v => v.IsDefault = false);
        }

        variant.GroupName = dto.GroupName.Trim();
        variant.Value = dto.Value.Trim();
        variant.PriceAdjustment = dto.PriceAdjustment;
        variant.Stock = dto.Stock;
        variant.IsDefault = dto.IsDefault;
        variant.SortOrder = dto.SortOrder;

        await _db.SaveChangesAsync();
        return Ok(variant);
    }

    [HttpDelete("admin/variants/{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var variant = await _db.ProductVariants.FindAsync(id);
        if (variant == null) return NotFound();
        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public class VariantDto
{
    public string GroupName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal PriceAdjustment { get; set; } = 0;
    public int Stock { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}
