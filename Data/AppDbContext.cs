using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Models;

namespace AdalyaSolarAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<ReviewLike> ReviewLikes { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SavedCard> SavedCards { get; set; }
    public DbSet<NewsletterSubscription> NewsletterSubscriptions { get; set; }
    public DbSet<StockNotificationRequest> StockNotificationRequests { get; set; }
    public DbSet<SiteSetting> SiteSettings { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<CampaignJoin> CampaignJoins { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<UserSecurityLog> UserSecurityLogs { get; set; }
    public DbSet<BlogPost> BlogPosts { get; set; }
    public DbSet<ReturnRequest> ReturnRequests { get; set; }
    public DbSet<ProductQuestion> ProductQuestions { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<AbandonedCart> AbandonedCarts { get; set; }
    public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }
    public DbSet<Bundle> Bundles { get; set; }
    public DbSet<BundleItem> BundleItems { get; set; }
    public DbSet<GiftCard> GiftCards { get; set; }
    public DbSet<ProductDocument> ProductDocuments { get; set; }
    public DbSet<PushSubscription> PushSubscriptions { get; set; }
    public DbSet<WarrantyRegistration> WarrantyRegistrations { get; set; }
    public DbSet<InstallationRequest> InstallationRequests { get; set; }
    public DbSet<QuoteRequest> QuoteRequests { get; set; }
    public DbSet<ProjectReference> ProjectReferences { get; set; }
    public DbSet<BulkOrderRequest> BulkOrderRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Kullanıcı başına ürün favori tekrarı engelle
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.ProductId })
            .IsUnique();

        // Kupon kodu benzersiz olsun
        modelBuilder.Entity<Coupon>()
            .HasIndex(c => c.Code)
            .IsUnique();

        // Kategori slug benzersiz olsun
        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        // Kullanıcı aynı kampanyaya iki kez katılamasın
        modelBuilder.Entity<CampaignJoin>()
            .HasIndex(c => new { c.UserId, c.CampaignId })
            .IsUnique();

        // Kullanıcı başına yorum beğeni tekrarı engelle
        modelBuilder.Entity<ReviewLike>()
            .HasIndex(r => new { r.ReviewId, r.UserId })
            .IsUnique();
    }
}
