namespace AdalyaSolarAPI.Models;

public class BulkOrderRequest
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    // JSON array: [{ productId, productName, quantity, note }]
    public string ItemsJson { get; set; } = "[]";

    public string DeliveryAddress { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public string Status { get; set; } = "beklemede"; // beklemede, inceleniyor, teklif-gonderildi, tamamlandi, reddedildi
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
