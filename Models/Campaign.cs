namespace AdalyaSolarAPI.Models;

public class Campaign
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Discount { get; set; } = "";
    public string Description { get; set; } = "";
    public string EndDate { get; set; } = "";
    public string GradientFrom { get; set; } = "#f97316";
    public string GradientTo { get; set; } = "#ea6c0a";
    public string Href { get; set; } = "/urunler";
    public string HrefLabel { get; set; } = "Ürünlere Git";
    public string Badge { get; set; } = "";
    public string BadgeBg { get; set; } = "rgba(255,255,255,0.2)";
    public string? CouponCode { get; set; }
    public string Icon { get; set; } = "Tag"; // Zap, Package, Gift, Star, Tag
    public string IconClass { get; set; } = "text-yellow-300";
    // "join" | "registered" | "corporate_contact"
    public string Requirement { get; set; } = "join";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
