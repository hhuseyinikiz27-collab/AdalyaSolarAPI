namespace AdalyaSolarAPI.Models;

public class InstallationRequest
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string InstallationType { get; set; } = string.Empty; // konut, isyeri, ciftlik, diger
    public string SystemSize { get; set; } = string.Empty; // e.g. "5 kW", "10 kW"
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = "beklemede"; // beklemede, inceleniyor, tamamlandi, reddedildi
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
