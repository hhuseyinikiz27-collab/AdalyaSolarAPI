namespace AdalyaSolarAPI.Models;

public class Bundle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal BundlePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<BundleItem> Items { get; set; } = new List<BundleItem>();
}

public class BundleItem
{
    public int Id { get; set; }
    public int BundleId { get; set; }
    public Bundle Bundle { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}
