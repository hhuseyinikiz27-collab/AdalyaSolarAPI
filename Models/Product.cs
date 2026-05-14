namespace AdalyaSolarAPI.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int Stock { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // JSON: [{"minQty":5,"discountPct":5},{"minQty":10,"discountPct":10}]
    public string? VolumeDiscountsJson { get; set; }
    public decimal? FlashSalePrice { get; set; }
    public DateTime? FlashSaleEndsAt { get; set; }
    public int WarrantyMonths { get; set; } = 24;
}
