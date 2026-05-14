using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdalyaSolarAPI.Data;

namespace AdalyaSolarAPI.Controllers;

public class ValidateCouponDto
{
    public string Code { get; set; } = "";
}

[ApiController]
[Route("api/coupons")]
public class CouponsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public CouponsController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateCouponDto dto)
    {
        var code = dto?.Code ?? "";
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Kupon kodu boş olamaz." });

        var normalizedCode = code.Trim().ToUpper();

        var coupon = await _db.Coupons
            .FirstOrDefaultAsync(c => c.Code == normalizedCode && c.IsActive);

        if (coupon == null)
        {
            // Kampanya tablosunda bu kod var mı? Varsa ama Coupons tablosunda yoksa admin tanımlamamış demektir.
            var campaignWithCode = await _db.Campaigns
                .FirstOrDefaultAsync(c => c.CouponCode != null &&
                    c.CouponCode.ToUpper() == normalizedCode && c.IsActive);

            if (campaignWithCode != null)
                return NotFound(new { message = "Geçersiz kupon kodu." });

            return NotFound(new { message = "Geçersiz kupon kodu." });
        }

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Bu kuponun süresi dolmuştur." });
        if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
            return BadRequest(new { message = "Bu kupon kullanım limitine ulaşmıştır." });

        // Kampanyaya bağlı kupon: kullanıcının o kampanyaya katılmış olması gerekiyor
        if (coupon.CampaignId.HasValue)
        {
            var userId = GetUserIdFromToken();
            if (userId == null)
                return Unauthorized(new { message = "Bu kupon yalnızca kampanyaya katılmış üyelere özeldir. Lütfen giriş yapın." });

            var isEligible = await CheckEligibility(userId.Value, coupon.CampaignId.Value);
            if (!isEligible)
                return BadRequest(new { message = "Bu kuponu kullanmak için önce kampanyaya katılmanız gerekiyor." });
        }

        return Ok(new
        {
            coupon.Code,
            coupon.DiscountType,
            coupon.DiscountValue,
            coupon.MinOrderAmount,
        });
    }

    private int? GetUserIdFromToken()
    {
        var auth = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ")) return null;

        try
        {
            var token = auth["Bearer ".Length..];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero,
            }, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            return idClaim != null ? int.Parse(idClaim.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> CheckEligibility(int userId, int campaignId)
    {
        var campaign = await _db.Campaigns.FindAsync(campaignId);
        if (campaign == null) return false;

        var requirement = campaign.Requirement;

        if (requirement == "registered") return true;

        if (requirement == "corporate_contact")
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return false;
            return await _db.ContactMessages
                .AnyAsync(m => m.Email == user.Email && m.Subject.Contains("Kurumsal"));
        }

        return await _db.CampaignJoins
            .AnyAsync(j => j.UserId == userId && j.CampaignId == campaignId);
    }
}
