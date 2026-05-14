namespace AdalyaSolarAPI.Models;

public class AbandonedCart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string ItemsJson { get; set; } = "[]";
    public decimal TotalAmount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EmailSentAt { get; set; }
}
