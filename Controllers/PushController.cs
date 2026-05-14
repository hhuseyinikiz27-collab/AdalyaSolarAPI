using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using AdalyaSolarAPI.Services;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PushService _push;

    public PushController(AppDbContext db, PushService push)
    {
        _db = db;
        _push = push;
    }

    // Public VAPID key — frontend uses this to create subscriptions
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey() =>
        Ok(new { publicKey = _push.PublicKey });

    // Save or update a push subscription
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeDto dto)
    {
        int? userId = null;
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(idStr) && int.TryParse(idStr, out var uid)) userId = uid;

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existing != null)
        {
            existing.P256dh = dto.P256dh;
            existing.Auth = dto.Auth;
            existing.UpdatedAt = DateTime.UtcNow;
            if (userId.HasValue) existing.UserId = userId;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
            });
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "Abonelik kaydedildi." });
    }

    // Remove a subscription (unsubscribe)
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeDto dto)
    {
        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (sub != null)
        {
            _db.PushSubscriptions.Remove(sub);
            await _db.SaveChangesAsync();
        }
        return Ok();
    }

    // Send a test push to the calling user (dev/debug)
    [HttpPost("test")]
    [Authorize]
    public async Task<IActionResult> SendTest()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return Unauthorized();

        var subs = await _db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
        foreach (var sub in subs)
        {
            _ = _push.SendAsync(sub, "Adalya Solar", "Push bildirimleri aktif! 🎉", "/");
        }
        return Ok(new { sent = subs.Count });
    }
}

public record PushSubscribeDto(string Endpoint, string P256dh, string Auth);
public record PushUnsubscribeDto(string Endpoint);
