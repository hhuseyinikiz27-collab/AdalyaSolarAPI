namespace AdalyaSolarAPI.Models;

public class ProjectReference
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Konut, Ticari, Endüstriyel, Tarımsal, Kamu
    public string Capacity { get; set; } = string.Empty; // e.g. "120 kWp"
    public int Panels { get; set; }
    public string Year { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Savings { get; set; } = string.Empty; // e.g. "18.000 ₺/ay"
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
