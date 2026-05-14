using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/public")]
public class PublicSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicSettingsController(AppDbContext db) { _db = db; }

    [HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
        return Ok(new { value = setting?.Value });
    }

    [HttpGet("site-info")]
    public async Task<IActionResult> GetSiteInfo()
    {
        var allowed = new[] { "site.", "social.", "tawkto." };
        var settings = await _db.SiteSettings
            .Where(s => allowed.Any(prefix => s.Key.StartsWith(prefix)))
            .ToListAsync();
        var dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return Ok(dict);
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns()
    {
        var campaigns = await _db.Campaigns
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id, c.Title, c.Subtitle, c.Discount, c.Description,
                c.EndDate, c.GradientFrom, c.GradientTo, c.Href, c.HrefLabel,
                c.Badge, c.BadgeBg, c.Icon, c.IconClass, c.Requirement, c.SortOrder,
                HasCoupon = c.CouponCode != null
            })
            .ToListAsync();
        return Ok(campaigns);
    }
}
