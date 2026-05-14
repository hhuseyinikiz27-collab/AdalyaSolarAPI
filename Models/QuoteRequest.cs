namespace AdalyaSolarAPI.Models;

public class QuoteRequest
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty; // konut, isyeri, ciftlik, diger
    public string SystemSize { get; set; } = string.Empty;
    public string Roof { get; set; } = string.Empty; // flat, sloped, ground
    public decimal? MonthlyBill { get; set; }
    public string Note { get; set; } = string.Empty;
    public string Status { get; set; } = "beklemede";
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
