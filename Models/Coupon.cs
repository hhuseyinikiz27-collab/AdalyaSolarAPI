namespace AdalyaSolarAPI.Models;

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "percentage"; // "percentage" | "fixed"
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // Dolu ise bu kuponu sadece ilgili kampanyaya katılmış kullanıcılar kullanabilir
    public int? CampaignId { get; set; }
}
