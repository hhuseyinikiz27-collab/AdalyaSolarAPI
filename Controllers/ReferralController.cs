using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/referral")]
[Authorize]
public class ReferralController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReferralController(AppDbContext db) => _db = db;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyReferral()
    {
        var uid = GetUserId();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return NotFound();

        if (string.IsNullOrEmpty(user.ReferralCode))
        {
            user.ReferralCode = GenerateCode(user.Name, uid);
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            code = user.ReferralCode,
            referralCount = user.ReferralCount,
            // her davet = 100 puan
            earnedPoints = user.ReferralCount * 100,
        });
    }

    // Anonim — kayıt sayfası kayıt öncesi kodu doğrular
    [HttpGet("validate/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate(string code)
    {
        var exists = await _db.Users.AnyAsync(u => u.ReferralCode == code);
        return Ok(new { valid = exists });
    }

    private static string GenerateCode(string name, int id)
    {
        var prefix = new string(name.ToUpperInvariant().Where(char.IsLetter).Take(4).ToArray());
        return $"{prefix}{id:D4}";
    }
}
