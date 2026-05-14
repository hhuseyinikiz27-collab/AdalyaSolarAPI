namespace AdalyaSolarAPI.Models;

public class SavedCard
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string CardHolderName { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string CardType { get; set; } = "Visa"; // Visa | Mastercard | Troy
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
