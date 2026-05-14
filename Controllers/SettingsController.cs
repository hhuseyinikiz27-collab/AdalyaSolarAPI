using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SettingsController(AppDbContext db) => _db = db;

    [HttpGet("shipping")]
    public async Task<IActionResult> GetShipping()
    {
        var settings = await _db.SiteSettings
            .Where(s => s.Key == "shipping.cost" || s.Key == "shipping.freeAbove")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        decimal cost      = decimal.TryParse(settings.GetValueOrDefault("shipping.cost",      "99"),  out var c) ? c : 99m;
        decimal freeAbove = decimal.TryParse(settings.GetValueOrDefault("shipping.freeAbove", "500"), out var f) ? f : 500m;

        return Ok(new { cost, freeAbove });
    }
}
