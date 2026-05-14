namespace AdalyaSolarAPI.Models;

public class LoyaltyTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int Points { get; set; }
    public string Type { get; set; } = "earned"; // "earned" | "redeemed"
    public int? OrderId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
