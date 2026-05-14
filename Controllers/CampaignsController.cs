using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public class CampaignsController : ControllerBase
{
    private readonly AppDbContext _db;
    public CampaignsController(AppDbContext db) => _db = db;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<bool> IsEligible(int userId, int campaignId, string requirement, string userEmail)
    {
        return requirement switch
        {
            "registered" => true,
            "corporate_contact" => await _db.ContactMessages
                .AnyAsync(m => m.Email == userEmail && m.Subject.Contains("Kurumsal")),
            _ => await _db.CampaignJoins
                .AnyAsync(j => j.UserId == userId && j.CampaignId == campaignId),
        };
    }

    [HttpGet("my-joins")]
    public async Task<IActionResult> GetMyJoins()
    {
        var userId = UserId;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Ok(new List<int>());

        var campaigns = await _db.Campaigns
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Requirement })
            .ToListAsync();

        var eligible = new List<int>();
        foreach (var c in campaigns)
        {
            if (await IsEligible(userId, c.Id, c.Requirement, user.Email))
                eligible.Add(c.Id);
        }

        return Ok(eligible);
    }

    [HttpGet("eligible-codes")]
    public async Task<IActionResult> GetEligibleCodes()
    {
        var userId = UserId;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Ok(new Dictionary<int, string>());

        var campaigns = await _db.Campaigns
            .Where(c => c.IsActive && c.CouponCode != null)
            .Select(c => new { c.Id, c.Requirement, c.CouponCode })
            .ToListAsync();

        var result = new Dictionary<int, string>();
        foreach (var c in campaigns)
        {
            if (await IsEligible(userId, c.Id, c.Requirement, user.Email))
                result[c.Id] = c.CouponCode!;
        }

        return Ok(result);
    }

    [HttpPost("{campaignId}/join")]
    public async Task<IActionResult> Join(int campaignId)
    {
        var campaign = await _db.Campaigns.FindAsync(campaignId);
        if (campaign == null) return NotFound();

        // Only "join" requirement campaigns can be joined via button
        if (campaign.Requirement != "join")
            return BadRequest(new { message = "Bu kampanyanın özel gereksinimleri var." });

        var already = await _db.CampaignJoins
            .AnyAsync(j => j.UserId == UserId && j.CampaignId == campaignId);

        if (already)
            return Ok(new { message = "Zaten katıldınız." });

        _db.CampaignJoins.Add(new CampaignJoin { UserId = UserId, CampaignId = campaignId });
        await _db.SaveChangesAsync();
        return Ok(new { message = "Kampanyaya başarıyla katıldınız!" });
    }
}
