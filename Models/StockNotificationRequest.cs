namespace AdalyaSolarAPI.Models;

public class StockNotificationRequest
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Notified { get; set; } = false;
}
