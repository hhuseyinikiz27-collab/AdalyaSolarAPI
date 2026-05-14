namespace AdalyaSolarAPI.Models;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    // Group label shown to user e.g. "Güç", "Renk", "Kapasite"
    public string GroupName { get; set; } = string.Empty;
    // Option value e.g. "300W", "Siyah", "10kWh"
    public string Value { get; set; } = string.Empty;
    // Price difference from base (can be negative)
    public decimal PriceAdjustment { get; set; } = 0;
    public int Stock { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}
