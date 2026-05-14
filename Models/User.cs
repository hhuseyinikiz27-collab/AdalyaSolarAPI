namespace AdalyaSolarAPI.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string Phone { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public string? LockoutReason { get; set; }
    public DateTime? SpamBanUntil { get; set; }
    public int LoyaltyPoints { get; set; } = 0;
    public string? GoogleId { get; set; }
    public string? ReferralCode { get; set; }
    public int ReferralCount { get; set; } = 0;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public bool FlashSaleNotify { get; set; } = false;
    public string? AdminNote { get; set; }
    public List<Order> Orders { get; set; } = new();
}
