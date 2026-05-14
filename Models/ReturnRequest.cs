namespace AdalyaSolarAPI.Models;

public class ReturnRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    // "iade" = return after delivery | "iptal" = late cancel request
    public string Type { get; set; } = "iade";
    public string Reason { get; set; } = string.Empty;
    // "beklemede" | "onaylandi" | "reddedildi" | "tamamlandi"
    public string Status { get; set; } = "beklemede";
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
