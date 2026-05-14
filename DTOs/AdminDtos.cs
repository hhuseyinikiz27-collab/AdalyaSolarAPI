namespace AdalyaSolarAPI.DTOs;

public record ProductCreateDto(
    string Name, string Description, decimal Price,
    string Category, string Brand, int Stock,
    bool IsFeatured, bool IsNew, decimal? DiscountPrice = null
);

public record ProductUpdateDto(
    string? Name, string? Description, decimal? Price,
    string? Category, string? Brand, int? Stock,
    bool? IsFeatured, bool? IsNew, decimal? DiscountPrice = null,
    bool ClearDiscount = false, string? VolumeDiscountsJson = null,
    decimal? FlashSalePrice = null, bool ClearFlashSale = false,
    DateTime? FlashSaleEndsAt = null, int? WarrantyMonths = null
);

public record CampaignDto(
    string Title, string Subtitle, string Discount, string Description,
    string EndDate, string GradientFrom, string GradientTo,
    string Href, string HrefLabel, string Badge, string BadgeBg,
    string? CouponCode, string Icon, string IconClass,
    string Requirement, bool IsActive, int SortOrder
);

public record ReviewCreateDto(string UserName, string UserEmail, int Rating, string Comment);

public record AddImageUrlDto(string Url);

public record CategoryDto(string Name, string Slug, string Icon, string? Description, int SortOrder);

public class UpdateOrderStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? TrackingCode { get; set; }
    public string? CargoCompany { get; set; }
}
