using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.DTOs;
using AdalyaSolarAPI.Models;
using AdalyaSolarAPI.Services;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly EmailService _email;
    private readonly AdalyaSolarAPI.Services.PushService _push;

    public AdminController(AppDbContext db, IWebHostEnvironment env, EmailService email, AdalyaSolarAPI.Services.PushService push)
    {
        _db = db;
        _env = env;
        _email = email;
        _push = push;
    }

    // ── PRODUCTS ──────────────────────────────────────────────────────────────

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _db.Products
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.ImageUrl,
                p.Category, p.Brand, p.Stock, p.IsFeatured, p.IsNew, p.CreatedAt,
                p.FlashSalePrice, p.FlashSaleEndsAt,
                FavoriteCount = _db.Favorites.Count(f => f.ProductId == p.Id),
            })
            .ToListAsync();
        return Ok(products);
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(ProductCreateDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Category = dto.Category,
            Brand = dto.Brand,
            Stock = dto.Stock,
            IsFeatured = dto.IsFeatured,
            IsNew = dto.IsNew,
            ImageUrl = "",
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(int id, ProductUpdateDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        decimal oldEffectivePrice = product.DiscountPrice ?? product.Price;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Stock.HasValue) product.Stock = dto.Stock.Value;
        if (dto.IsFeatured.HasValue) product.IsFeatured = dto.IsFeatured.Value;
        if (dto.IsNew.HasValue) product.IsNew = dto.IsNew.Value;
        if (dto.ClearDiscount) product.DiscountPrice = null;
        else if (dto.DiscountPrice.HasValue) product.DiscountPrice = dto.DiscountPrice.Value > 0 ? dto.DiscountPrice.Value : null;
        if (dto.VolumeDiscountsJson != null) product.VolumeDiscountsJson = dto.VolumeDiscountsJson == "null" ? null : dto.VolumeDiscountsJson;
        if (dto.ClearFlashSale) { product.FlashSalePrice = null; product.FlashSaleEndsAt = null; }
        else { if (dto.FlashSalePrice.HasValue) product.FlashSalePrice = dto.FlashSalePrice; if (dto.FlashSaleEndsAt.HasValue) product.FlashSaleEndsAt = dto.FlashSaleEndsAt; }
        if (dto.WarrantyMonths.HasValue) product.WarrantyMonths = dto.WarrantyMonths.Value;

        decimal newEffectivePrice = product.DiscountPrice ?? product.Price;
        await _db.SaveChangesAsync();

        // Stok düşük uyarısı
        var thresholdSetting = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == "stock.lowThreshold");
        int lowThreshold = int.TryParse(thresholdSetting?.Value, out var t) ? t : 5;
        if (dto.Stock.HasValue && product.Stock <= lowThreshold)
        {
            var admins = await _db.Users.Where(u => u.Role == "admin").ToListAsync();
            foreach (var admin in admins)
                _db.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = "Düşük Stok Uyarısı",
                    Message = product.Stock == 0
                        ? $"'{product.Name}' ürünü stoktan tükendi!"
                        : $"'{product.Name}' ürününde yalnızca {product.Stock} adet kaldı.",
                    Type = "admin",
                });
            if (admins.Count > 0) await _db.SaveChangesAsync();
        }

        // Fiyat düştüyse favorileyenlere bildirim gönder
        if (newEffectivePrice < oldEffectivePrice)
        {
            var favoriUsers = await _db.Favorites
                .Where(f => f.ProductId == id)
                .Include(f => f.User)
                .ToListAsync();

            foreach (var fav in favoriUsers)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = fav.UserId,
                    Title = "Fiyat Düştü!",
                    Message = $"Favorilerinizde bulunan \"{product.Name}\" ürününün fiyatı {oldEffectivePrice:N0} ₺'den {newEffectivePrice:N0} ₺'ye düştü!",
                    Type = "promo",
                });
                _ = _email.SendAsync(
                    fav.User.Email,
                    fav.User.Name,
                    $"Fiyat Düştü: {product.Name}",
                    $"Merhaba {fav.User.Name},\n\nFavorilerinize eklediğiniz \"{product.Name}\" ürününün fiyatı {oldEffectivePrice:N0} ₺'den {newEffectivePrice:N0} ₺'ye düştü!\n\nFırsatı kaçırmayın:\nhttps://adalyasolar.com/urunler/{id}\n\nAdalya Solar Enerji"
                );
            }
            if (favoriUsers.Count > 0) await _db.SaveChangesAsync();
        }

        return Ok(product);
    }

    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("products/{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsPath);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext)) return BadRequest("Sadece JPG, PNG veya WebP yükleyebilirsiniz.");

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        product.ImageUrl = $"/uploads/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { imageUrl = product.ImageUrl });
    }

    // ── SEED PRODUCT IMAGES ──────────────────────────────────────────────────

    [HttpPost("seed-images")]
    public async Task<IActionResult> SeedProductImages()
    {
        var pool = new Dictionary<string, string[]>
        {
            ["gunes-panelleri"] = new[]
            {
                "https://images.unsplash.com/photo-1509391366360-2e959784a276?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1508514177221-188b1cf16e9d?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1466611653911-95081537e5b7?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1611365892117-00ac5ef43c90?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1548337138-e87d889cc369?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1613665813446-82a78c468a1d?w=800&h=600&fit=crop",
            },
            ["bataryalar"] = new[]
            {
                "https://images.unsplash.com/photo-1593941707882-a5bba14938c7?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1609091839311-d5365f9ff1c5?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1530587191325-3db32d826c18?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1497435334941-8c899ee9e8e9?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1604594849809-dfedbc827105?w=800&h=600&fit=crop",
            },
            ["inverterler"] = new[]
            {
                "https://images.unsplash.com/photo-1548407260-da850faa41e3?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1621905251918-48416bd8575a?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1504328345606-18bbc8c9d7d1?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1581092918056-0c4c3acd3789?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1518770660439-4636190af475?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1558618047-3c8c76ca7d13?w=800&h=600&fit=crop",
            },
            ["montaj-aksesuarlari"] = new[]
            {
                "https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1589939705384-5185137a7f0f?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1541888946425-d81bb19240f5?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1504328345606-18bbc8c9d7d1?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1581092160607-ee22621dd758?w=800&h=600&fit=crop",
                "https://images.unsplash.com/photo-1484807352052-23338990c6c6?w=800&h=600&fit=crop",
            },
        };

        var products = await _db.Products.ToListAsync();
        int count = 0;

        foreach (var product in products)
        {
            // Zaten fotoğrafı olanları atla
            bool hasImages = await _db.ProductImages.AnyAsync(i => i.ProductId == product.Id);
            if (hasImages) continue;

            if (!pool.TryGetValue(product.Category, out var urls))
                urls = pool["gunes-panelleri"];

            // Her ürüne 3 fotoğraf ekle (ID'ye göre rotation)
            for (int i = 0; i < 3; i++)
            {
                var idx = (product.Id + i) % urls.Length;
                _db.ProductImages.Add(new ProductImage
                {
                    ProductId = product.Id,
                    Url = urls[idx],
                    SortOrder = i,
                });
            }
            count++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { seeded = count, message = $"{count} ürüne fotoğraf eklendi." });
    }

    // ── PRODUCT IMAGES ───────────────────────────────────────────────────────

    [HttpGet("products/{id}/images")]
    public async Task<IActionResult> GetProductImages(int id)
    {
        var images = await _db.ProductImages
            .Where(i => i.ProductId == id)
            .OrderBy(i => i.SortOrder)
            .Select(i => new { i.Id, i.Url, i.SortOrder })
            .ToListAsync();
        return Ok(images);
    }

    [HttpPost("products/{id}/images/url")]
    public async Task<IActionResult> AddImageByUrl(int id, [FromBody] AddImageUrlDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var maxOrder = await _db.ProductImages
            .Where(i => i.ProductId == id)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync() ?? -1;

        var img = new ProductImage { ProductId = id, Url = dto.Url, SortOrder = maxOrder + 1 };
        _db.ProductImages.Add(img);
        await _db.SaveChangesAsync();
        return Ok(new { img.Id, img.Url, img.SortOrder });
    }

    [HttpPost("products/{id}/images/upload")]
    public async Task<IActionResult> UploadProductImage(int id, IFormFile file)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsPath);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext)) return BadRequest("Sadece JPG, PNG veya WebP yükleyebilirsiniz.");

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var maxOrder = await _db.ProductImages
            .Where(i => i.ProductId == id)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync() ?? -1;

        var img = new ProductImage { ProductId = id, Url = $"/uploads/{fileName}", SortOrder = maxOrder + 1 };
        _db.ProductImages.Add(img);
        await _db.SaveChangesAsync();
        return Ok(new { img.Id, img.Url, img.SortOrder });
    }

    [HttpDelete("products/{productId}/images/{imageId}")]
    public async Task<IActionResult> DeleteProductImage(int productId, int imageId)
    {
        var img = await _db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId);
        if (img == null) return NotFound();
        _db.ProductImages.Remove(img);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── USERS ────────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
        => Ok(await _db.Users
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.CreatedAt, u.AdminNote, OrderCount = u.Orders.Count })
            .ToListAsync());

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, [FromBody] string role)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.Role = role;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("users/{id}/orders")]
    public async Task<IActionResult> GetUserOrders(int id)
        => Ok(await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.UserId == id)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.Total,
                o.Status,
                o.ShippingFullName,
                o.ShippingAddress,
                o.Note,
                o.CreatedAt,
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice,
                    ProductName = i.Product.Name,
                })
            })
            .ToListAsync());

    // ── ORDERS ───────────────────────────────────────────────────────────────

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
        => Ok(await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.Total,
                o.Status,
                o.ShippingFullName,
                o.ShippingPhone,
                o.ShippingAddress,
                o.Note,
                o.TrackingCode,
                o.CargoCompany,
                o.AdminNote,
                o.CreatedAt,
                User = o.User == null ? null : new { o.User.Name, o.User.Email },
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice,
                    ProductName = i.Product.Name,
                    ProductImageUrl = i.Product.ImageUrl,
                }).ToList(),
            })
            .ToListAsync());

    [HttpPut("orders/{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.Status = dto.Status;
        if (dto.TrackingCode != null) order.TrackingCode = dto.TrackingCode;
        if (dto.CargoCompany != null) order.CargoCompany = dto.CargoCompany;
        await _db.SaveChangesAsync();

        var statusMessages = new Dictionary<string, (string Title, string Msg)>
        {
            ["hazirlanıyor"]    = ("Siparişiniz Hazırlanıyor", $"#{order.Id} numaralı siparişiniz hazırlanmaya başlandı."),
            ["kargoya-verildi"] = ("Siparişiniz Kargoya Verildi", $"#{order.Id} numaralı siparişiniz kargoya verildi." + (dto.CargoCompany != null ? $" Kargo: {dto.CargoCompany}" : "") + (dto.TrackingCode != null ? $" | Takip: {dto.TrackingCode}" : "")),
            ["dagitimda"]       = ("Siparişiniz Dağıtımda", $"#{order.Id} numaralı siparişiniz bugün teslim edilecek."),
            ["teslim-edildi"]   = ("Siparişiniz Teslim Edildi", $"#{order.Id} numaralı siparişiniz teslim edildi. İyi kullanımlar!"),
            ["iptal"]           = ("Sipariş İptal Edildi", $"#{order.Id} numaralı siparişiniz iptal edildi."),
        };

        if (statusMessages.TryGetValue(dto.Status, out var notif))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = order.UserId,
                Title = notif.Title,
                Message = notif.Msg,
                Type = "order",
            });
            await _db.SaveChangesAsync();

            var user = await _db.Users.FindAsync(order.UserId);
            if (user != null)
            {
                _ = _email.SendAsync(user.Email, user.Name, notif.Title,
                    notif.Msg + $"\n\nSiparişlerinizi görüntülemek için: https://adalyasolar.com/hesabim");

                // Web Push
                var pushSubs = await _db.PushSubscriptions
                    .Where(s => s.UserId == order.UserId)
                    .ToListAsync();
                _ = _push.SendToAllAsync(pushSubs, notif.Title, notif.Msg, "/hesabim?tab=siparisler", "order");
            }
        }

        return Ok();
    }

    [HttpGet("orders/new-count")]
    public async Task<IActionResult> NewOrdersCount()
    {
        var count = await _db.Orders.CountAsync(o => o.Status == "hazirlanıyor");
        return Ok(new { count });
    }

    // ── REVIEWS ──────────────────────────────────────────────────────────────

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews()
        => Ok(await _db.Reviews
            .Include(r => r.Product)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id, r.UserName, r.UserEmail, r.Rating, r.Comment,
                r.AdminReply, r.CreatedAt, r.LikeCount,
                ProductName = r.Product.Name, r.ProductId
            })
            .ToListAsync());

    [HttpPost("reviews/{id}/reply")]
    public async Task<IActionResult> ReplyToReview(int id, [FromBody] ReviewReplyDto dto)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();
        review.AdminReply = dto.Reply;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("reviews/{id}")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null) return NotFound();
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── NEWSLETTER ──────────────────────────────────────────────────────────────

    [HttpGet("newsletter/subscribers")]
    public async Task<IActionResult> GetSubscribers()
        => Ok(await _db.NewsletterSubscriptions.Where(s => s.IsActive).OrderByDescending(s => s.CreatedAt).ToListAsync());

    // ── CATEGORIES ──────────────────────────────────────────────────────────────

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
        => Ok(await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
    {
        var cat = new AdalyaSolarAPI.Models.Category
        {
            Name = dto.Name, Slug = dto.Slug, Icon = dto.Icon,
            Description = dto.Description ?? "", SortOrder = dto.SortOrder,
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto dto)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        cat.Name = dto.Name; cat.Slug = dto.Slug; cat.Icon = dto.Icon;
        cat.Description = dto.Description ?? ""; cat.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(cat);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── COUPONS ──────────────────────────────────────────────────────────────

    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons()
        => Ok(await _db.Coupons.OrderByDescending(c => c.CreatedAt).ToListAsync());

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] CouponDto dto)
    {
        if (await _db.Coupons.AnyAsync(c => c.Code == dto.Code.ToUpper()))
            return BadRequest(new { message = "Bu kupon kodu zaten mevcut." });

        var coupon = new AdalyaSolarAPI.Models.Coupon
        {
            Code = dto.Code.Trim().ToUpper(),
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            MinOrderAmount = dto.MinOrderAmount,
            MaxUses = dto.MaxUses,
            ExpiresAt = dto.ExpiresAt,
            CampaignId = dto.CampaignId,
            IsActive = true,
        };
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();
        return Ok(coupon);
    }

    [HttpPut("coupons/{id}/toggle")]
    public async Task<IActionResult> ToggleCoupon(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();
        coupon.IsActive = !coupon.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { coupon.Id, coupon.Code, coupon.IsActive });
    }

    [HttpDelete("coupons/{id}")]
    public async Task<IActionResult> DeleteCoupon(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();
        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── DASHBOARD ────────────────────────────────────────────────────────────

    [HttpGet("stats/monthly")]
    public async Task<IActionResult> GetMonthlyStats([FromQuery] int months = 6)
    {
        if (months < 1) months = 1;
        if (months > 24) months = 24;
        var from = DateTime.UtcNow.AddMonths(-months);
        var data = await _db.Orders
            .Where(o => o.CreatedAt >= from && o.Status != "iptal")
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(o => o.Total), Count = g.Count() })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("stats/daily")]
    public async Task<IActionResult> GetDailyStats([FromQuery] int months = 1)
    {
        if (months < 1) months = 1;
        if (months > 3) months = 3;
        var from = DateTime.UtcNow.AddMonths(-months);
        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= from && o.Status != "iptal")
            .ToListAsync();
        var data = orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.Total), Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToList();
        return Ok(data);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = new
        {
            TotalProducts = await _db.Products.CountAsync(),
            TotalUsers = await _db.Users.CountAsync(),
            TotalOrders = await _db.Orders.CountAsync(),
            TotalRevenue = await _db.Orders.Where(o => o.Status != "iptal").SumAsync(o => (decimal?)o.Total) ?? 0,
            TotalReviews = await _db.Reviews.CountAsync(),
            TotalFavorites = await _db.Favorites.CountAsync(),
            TotalLikes = await _db.ReviewLikes.CountAsync(),
        };
        return Ok(stats);
    }

    // ── SETTINGS ──────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _db.SiteSettings.ToListAsync();
        var dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return Ok(dict);
    }

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] Dictionary<string, string> data)
    {
        foreach (var (key, value) in data)
        {
            var existing = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
            }
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "Ayarlar kaydedildi." });
    }

    // ── CAMPAIGNS ───────────────────────────────────────────────────────────────

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns()
        => Ok(await _db.Campaigns.OrderBy(c => c.SortOrder).ThenByDescending(c => c.Id).ToListAsync());

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignDto dto)
    {
        var c = new AdalyaSolarAPI.Models.Campaign
        {
            Title = dto.Title, Subtitle = dto.Subtitle, Discount = dto.Discount,
            Description = dto.Description, EndDate = dto.EndDate,
            GradientFrom = dto.GradientFrom, GradientTo = dto.GradientTo,
            Href = dto.Href, HrefLabel = dto.HrefLabel,
            Badge = dto.Badge, BadgeBg = dto.BadgeBg, CouponCode = dto.CouponCode,
            Icon = dto.Icon, IconClass = dto.IconClass, Requirement = dto.Requirement,
            IsActive = dto.IsActive, SortOrder = dto.SortOrder,
        };
        _db.Campaigns.Add(c);
        await _db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpPut("campaigns/{id}")]
    public async Task<IActionResult> UpdateCampaign(int id, [FromBody] CampaignDto dto)
    {
        var c = await _db.Campaigns.FindAsync(id);
        if (c == null) return NotFound();
        c.Title = dto.Title; c.Subtitle = dto.Subtitle; c.Discount = dto.Discount;
        c.Description = dto.Description; c.EndDate = dto.EndDate;
        c.GradientFrom = dto.GradientFrom; c.GradientTo = dto.GradientTo;
        c.Href = dto.Href; c.HrefLabel = dto.HrefLabel;
        c.Badge = dto.Badge; c.BadgeBg = dto.BadgeBg; c.CouponCode = dto.CouponCode;
        c.Icon = dto.Icon; c.IconClass = dto.IconClass; c.Requirement = dto.Requirement;
        c.IsActive = dto.IsActive; c.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(c);
    }

    [HttpDelete("campaigns/{id}")]
    public async Task<IActionResult> DeleteCampaign(int id)
    {
        var c = await _db.Campaigns.FindAsync(id);
        if (c == null) return NotFound();
        _db.Campaigns.Remove(c);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── CONTACT MESSAGES ────────────────────────────────────────────────────────

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages()
    {
        var msgs = await _db.ContactMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        return Ok(msgs);
    }

    [HttpGet("messages/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await _db.ContactMessages.CountAsync(m => !m.IsRead);
        return Ok(new { count });
    }

    [HttpGet("reviews/unanswered-count")]
    public async Task<IActionResult> GetUnansweredReviewsCount()
    {
        var count = await _db.Reviews.CountAsync(r => string.IsNullOrEmpty(r.AdminReply));
        return Ok(new { count });
    }

    [HttpGet("coupons/expiring-count")]
    public async Task<IActionResult> GetExpiringCouponsCount()
    {
        var threeDaysLater = DateTime.UtcNow.AddDays(3);
        var count = await _db.Coupons.CountAsync(c =>
            c.IsActive && c.ExpiresAt.HasValue &&
            c.ExpiresAt >= DateTime.UtcNow && c.ExpiresAt <= threeDaysLater);
        return Ok(new { count });
    }

    [HttpPut("messages/{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();
        msg.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("messages/{id}")]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var msg = await _db.ContactMessages.FindAsync(id);
        if (msg == null) return NotFound();
        _db.ContactMessages.Remove(msg);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── USER SECURITY ────────────────────────────────────────────────────────────

    [HttpGet("users/{id}/security")]
    public async Task<IActionResult> GetUserSecurity(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var logs = await _db.UserSecurityLogs
            .Where(l => l.UserId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .Select(l => new { l.Id, l.Action, l.Details, l.CreatedAt })
            .ToListAsync();

        return Ok(new
        {
            user.LastLoginAt,
            user.PasswordChangedAt,
            user.LockoutUntil,
            user.LockoutReason,
            IsLocked = user.LockoutUntil.HasValue && user.LockoutUntil > DateTime.UtcNow,
            user.SpamBanUntil,
            IsSpamBanned = user.SpamBanUntil.HasValue && user.SpamBanUntil > DateTime.UtcNow,
            Logs = logs,
        });
    }

    [HttpPut("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.LockoutUntil = null;
        user.LockoutReason = null;

        _db.UserSecurityLogs.Add(new AdalyaSolarAPI.Models.UserSecurityLog
        {
            UserId = id,
            Action = "admin_unlocked",
            Details = "Admin tarafından kilit kaldırıldı."
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Kullanıcı kilidi kaldırıldı." });
    }

    [HttpPut("users/{id}/clear-spam-ban")]
    public async Task<IActionResult> ClearSpamBan(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.SpamBanUntil = null;

        _db.UserSecurityLogs.Add(new AdalyaSolarAPI.Models.UserSecurityLog
        {
            UserId = id,
            Action = "admin_unlocked",
            Details = "Admin tarafından sipariş kısıtlaması kaldırıldı."
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Sipariş kısıtlaması kaldırıldı." });
    }

    // ── SECURITY SETTINGS ────────────────────────────────────────────────────────

    [HttpGet("security-settings")]
    public async Task<IActionResult> GetSecuritySettings()
    {
        var keys = new[]
        {
            "security.passwordLockoutMinutes",
            "security.rapidChangeWindowMinutes",
            "security.rapidChangeLimit",
            "security.orderSpamLimitPerHour",
            "security.orderSpamBanMinutes",
        };

        var defaults = new Dictionary<string, string>
        {
            ["security.passwordLockoutMinutes"] = "30",
            ["security.rapidChangeWindowMinutes"] = "60",
            ["security.rapidChangeLimit"] = "2",
            ["security.orderSpamLimitPerHour"] = "5",
            ["security.orderSpamBanMinutes"] = "30",
        };

        var saved = await _db.SiteSettings
            .Where(s => s.Key.StartsWith("security."))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        // Kayıtlı yoksa varsayılanları döndür
        var result = new Dictionary<string, string>();
        foreach (var key in keys)
            result[key] = saved.TryGetValue(key, out var v) ? v : defaults[key];

        return Ok(result);
    }

    [HttpPost("security-settings")]
    public async Task<IActionResult> SaveSecuritySettings([FromBody] Dictionary<string, string> data)
    {
        foreach (var kv in data)
        {
            if (!kv.Key.StartsWith("security.")) continue;
            var setting = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == kv.Key);
            if (setting == null)
                _db.SiteSettings.Add(new AdalyaSolarAPI.Models.SiteSetting { Key = kv.Key, Value = kv.Value });
            else
                setting.Value = kv.Value;
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── LOYALTY: İade/İptal Puan Geri Alma ───────────────────────────────────

    [HttpPost("loyalty/deduct-for-order/{orderId}")]
    public async Task<IActionResult> DeductLoyaltyForOrder(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        var earnedTx = await _db.LoyaltyTransactions
            .FirstOrDefaultAsync(t => t.OrderId == orderId && t.Points > 0);
        if (earnedTx == null) return Ok();

        var user = await _db.Users.FindAsync(order.UserId);
        if (user == null) return Ok();

        user.LoyaltyPoints = Math.Max(0, user.LoyaltyPoints - earnedTx.Points);
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            UserId      = order.UserId,
            OrderId     = orderId,
            Points      = -earnedTx.Points,
            Type        = "deduct",
            Description = $"Sipariş #{orderId} iade/iptal — puan geri alındı",
            CreatedAt   = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // ── FLASH SALE: Favorileyenlere Bildirim + Email ───────────────────────────

    [HttpPost("products/{id}/notify-flash-sale")]
    public async Task<IActionResult> NotifyFlashSale(int id, [FromBody] FlashSaleNotifyDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var discount = (int)Math.Round((1 - (double)dto.FlashPrice / (double)product.Price) * 100);
        var siteUrl  = "https://adalyasolar.com";

        // Slug frontend formatıyla aynı: name-kebab-id
        var slug = System.Text.RegularExpressions.Regex
            .Replace(product.Name.ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-') + "-" + id;

        var endsAtLocal = dto.EndsAt.ToLocalTime().ToString("dd MMMM yyyy HH:mm",
            new System.Globalization.CultureInfo("tr-TR"));

        // Favorileyenler
        var favUserIds = await _db.Favorites
            .Where(f => f.ProductId == id)
            .Select(f => f.UserId)
            .ToListAsync();

        // Flash sale bildirimi açık kullanıcılar (favorileyen değilse ekle)
        var optInUserIds = await _db.Users
            .Where(u => u.FlashSaleNotify && u.Role != "admin" && !favUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var allTargetIds = favUserIds.Concat(optInUserIds).Distinct().ToList();

        var targetUsers = await _db.Users
            .Where(u => allTargetIds.Contains(u.Id))
            .ToListAsync();

        foreach (var target in targetUsers)
        {
            var isFav = favUserIds.Contains(target.Id);
            var message = isFav
                ? $"Favorilerinizde bulunan \"{product.Name}\" ürününde %{discount} flash indirim başladı! Fiyat: {dto.FlashPrice:N0} ₺"
                : $"\"{product.Name}\" ürününde %{discount} flash indirim başladı! Fiyat: {dto.FlashPrice:N0} ₺";

            _db.Notifications.Add(new Notification
            {
                UserId    = target.Id,
                Title     = $"⚡ Flash İndirim: {product.Name}",
                Message   = message,
                Type      = "promo",
                CreatedAt = DateTime.UtcNow,
            });

            var html = BuildFlashSaleHtml(target.Name, product.Name, slug, dto.FlashPrice, product.Price, discount, endsAtLocal, siteUrl);
            _ = _email.SendHtmlAsync(
                target.Email,
                target.Name,
                $"⚡ Flash İndirim: {product.Name} — Adalya Solar",
                html
            );
        }

        // Admin onay bildirimi
        var admins = await _db.Users.Where(u => u.Role == "admin").ToListAsync();
        foreach (var admin in admins)
            _db.Notifications.Add(new Notification
            {
                UserId    = admin.Id,
                Title     = "Flash İndirim Başlatıldı",
                Message   = $"\"{product.Name}\" ürününde %{discount} flash indirim başlatıldı. {targetUsers.Count} kullanıcıya bildirim gönderildi.",
                Type      = "info",
                CreatedAt = DateTime.UtcNow,
            });

        await _db.SaveChangesAsync();

        // Web Push — tüm hedef kullanıcıların aboneliklerine gönder
        var targetUserIds = targetUsers.Select(u => u.Id).ToList();
        var pushSubs = await _db.PushSubscriptions
            .Where(s => s.UserId.HasValue && targetUserIds.Contains(s.UserId.Value))
            .ToListAsync();
        _ = _push.SendToAllAsync(pushSubs,
            "⚡ Flash İndirim Başladı!",
            $"{product.Name} — %{discount} indirim! Sadece sınırlı süre.",
            $"/urunler/{slug}",
            "flash-sale");

        var users = targetUsers.Select(u => new { name = u.Name, email = u.Email }).ToList();
        return Ok(new { productSlug = slug, users });
    }

    // ── ANALYTICS: TOP PRODUCTS ──────────────────────────────────────────────

    [HttpGet("stats/top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] int days = 30, [FromQuery] int take = 10)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var raw = await _db.OrderItems
            .Include(i => i.Product)
            .Include(i => i.Order)
            .Where(i => i.Order.CreatedAt >= from && i.Order.Status != "iptal")
            .ToListAsync();

        var top = raw
            .GroupBy(i => new { i.ProductId, ProductName = i.Product?.Name ?? "?" })
            .Select(g => new {
                productId = g.Key.ProductId,
                productName = g.Key.ProductName,
                totalQuantity = g.Sum(i => i.Quantity),
                totalRevenue = g.Sum(i => i.Quantity * i.UnitPrice),
                orderCount = g.Select(i => i.OrderId).Distinct().Count(),
            })
            .OrderByDescending(x => x.totalRevenue)
            .Take(take)
            .ToList();

        return Ok(top);
    }

    // ── CSV EXPORT ──────────────────────────────────────────────────────────

    [HttpGet("export/orders")]
    public async Task<IActionResult> ExportOrders()
    {
        var orders = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Sipariş ID,Müşteri Adı,Email,Toplam (₺),Durum,Kargo Firması,Takip Kodu,Adres,Ürünler,Tarih");
        foreach (var o in orders)
        {
            var items = string.Join(" | ", o.Items.Select(i => $"{i.Product?.Name ?? "?"} x{i.Quantity}"));
            sb.AppendLine(CsvRow(
                o.Id.ToString(), o.ShippingFullName,
                o.User?.Email ?? "", o.Total.ToString("N2"),
                o.Status, o.CargoCompany ?? "", o.TrackingCode ?? "",
                o.ShippingAddress, items, o.CreatedAt.ToString("yyyy-MM-dd HH:mm")));
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"siparisler_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/customers")]
    public async Task<IActionResult> ExportCustomers()
    {
        var users = await _db.Users
            .Where(u => u.Role != "admin")
            .Select(u => new {
                u.Id, u.Name, u.Email, u.Phone, u.LoyaltyPoints, u.ReferralCount, u.CreatedAt, u.LastLoginAt,
                OrderCount = _db.Orders.Count(o => o.UserId == u.Id),
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID,Ad Soyad,Email,Telefon,Puan,Sipariş Sayısı,Referans Sayısı,Kayıt Tarihi,Son Giriş");
        foreach (var u in users)
        {
            sb.AppendLine(CsvRow(
                u.Id.ToString(), u.Name, u.Email, u.Phone,
                u.LoyaltyPoints.ToString(), u.OrderCount.ToString(), u.ReferralCount.ToString(),
                u.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                u.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? ""));
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"musteriler_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/products")]
    public async Task<IActionResult> ExportProducts()
    {
        var products = await _db.Products
            .Select(p => new {
                p.Id, p.Name, p.Category, p.Brand, p.Price, p.DiscountPrice, p.Stock, p.IsFeatured, p.IsNew, p.CreatedAt,
                SoldCount = _db.OrderItems.Where(i => i.ProductId == p.Id).Sum(i => (int?)i.Quantity) ?? 0,
            })
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID,Ürün Adı,Kategori,Marka,Fiyat (₺),İndirimli Fiyat (₺),Stok,Satılan,Öne Çıkan,Yeni,Eklenme Tarihi");
        foreach (var p in products)
        {
            sb.AppendLine(CsvRow(
                p.Id.ToString(), p.Name, p.Category, p.Brand,
                p.Price.ToString("N2"), p.DiscountPrice?.ToString("N2") ?? "",
                p.Stock.ToString(), p.SoldCount.ToString(),
                p.IsFeatured ? "Evet" : "Hayır", p.IsNew ? "Evet" : "Hayır",
                p.CreatedAt.ToString("yyyy-MM-dd")));
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"urunler_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string CsvRow(params string[] fields)
        => string.Join(",", fields.Select(f => $"\"{f.Replace("\"", "\"\"")}\""));

    // ── BULK PRODUCT UPDATE ──────────────────────────────────────────────────

    [HttpPost("products/bulk-update")]
    public async Task<IActionResult> BulkUpdateProducts([FromBody] BulkProductUpdateDto dto)
    {
        if (dto.Ids == null || dto.Ids.Count == 0)
            return BadRequest(new { message = "Ürün seçilmedi." });

        var products = await _db.Products.Where(p => dto.Ids.Contains(p.Id)).ToListAsync();
        foreach (var product in products)
        {
            if (dto.Price.HasValue) product.Price = dto.Price.Value;
            if (dto.ClearDiscount == true) product.DiscountPrice = null;
            else if (dto.DiscountPrice.HasValue) product.DiscountPrice = dto.DiscountPrice.Value > 0 ? dto.DiscountPrice.Value : null;
            if (dto.Stock.HasValue) product.Stock = dto.Stock.Value;
            if (dto.IsFeatured.HasValue) product.IsFeatured = dto.IsFeatured.Value;
            if (dto.IsNew.HasValue) product.IsNew = dto.IsNew.Value;
        }
        await _db.SaveChangesAsync();
        return Ok(new { updated = products.Count });
    }

    // ── USER NOTE ──────────────────────────────────────────────────────────

    [HttpGet("users/{id}/note")]
    public async Task<IActionResult> GetUserNote(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Ok(new { note = user.AdminNote });
    }

    [HttpPut("users/{id}/note")]
    public async Task<IActionResult> UpdateUserNote(int id, [FromBody] AdminNoteDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.AdminNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { note = user.AdminNote });
    }

    // ── ORDER NOTE ──────────────────────────────────────────────────────────

    [HttpGet("orders/{id}/note")]
    public async Task<IActionResult> GetOrderNote(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();
        return Ok(new { note = order.AdminNote });
    }

    [HttpPut("orders/{id}/note")]
    public async Task<IActionResult> UpdateOrderNote(int id, [FromBody] AdminNoteDto dto)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();
        order.AdminNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { note = order.AdminNote });
    }

    // ── PRODUCT DOCUMENTS ─────────────────────────────────────────────────────

    [HttpGet("products/{id}/documents")]
    public async Task<IActionResult> GetProductDocuments(int id)
    {
        var docs = await _db.ProductDocuments
            .Where(d => d.ProductId == id)
            .OrderBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.Name, d.FileUrl, d.FileType, d.SizeBytes, d.SortOrder, d.CreatedAt })
            .ToListAsync();
        return Ok(docs);
    }

    [HttpPost("products/{id}/documents")]
    public async Task<IActionResult> UploadProductDocument(int id, IFormFile file, [FromForm] string name)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();

        var allowed = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".dwg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Desteklenmeyen dosya türü. PDF, DOC, XLS, ZIP veya DWG yükleyebilirsiniz." });

        var uploadsPath = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "documents");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"doc_{id}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var maxOrder = await _db.ProductDocuments.Where(d => d.ProductId == id).Select(d => (int?)d.SortOrder).MaxAsync() ?? -1;

        var doc = new AdalyaSolarAPI.Models.ProductDocument
        {
            ProductId = id,
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name,
            FileUrl = $"/uploads/documents/{fileName}",
            FileType = ext.TrimStart('.'),
            SizeBytes = file.Length,
            SortOrder = maxOrder + 1,
        };
        _db.ProductDocuments.Add(doc);
        await _db.SaveChangesAsync();

        return Ok(new { doc.Id, doc.Name, doc.FileUrl, doc.FileType, doc.SizeBytes, doc.SortOrder });
    }

    [HttpDelete("products/{productId}/documents/{docId}")]
    public async Task<IActionResult> DeleteProductDocument(int productId, int docId)
    {
        var doc = await _db.ProductDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ProductId == productId);
        if (doc == null) return NotFound();

        var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot", doc.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

        _db.ProductDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return Ok();
    }

    private static string BuildFlashSaleHtml(
        string customerName, string productName, string slug,
        decimal flashPrice, decimal originalPrice, int discount,
        string endsAt, string siteUrl)
    {
        return $@"<!DOCTYPE html><html lang=""tr""><head><meta charset=""UTF-8"">
<style>
  body{{font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:20px}}
  .card{{background:#fff;max-width:600px;margin:0 auto;border-radius:8px;overflow:hidden}}
  .header{{background:#dc2626;padding:24px;text-align:center}}
  .header h1{{color:#fff;margin:0;font-size:20px}}
  .body{{padding:28px}}
  .price-box{{background:#fef2f2;border:2px solid #dc2626;border-radius:8px;padding:20px;text-align:center;margin:20px 0}}
  .btn{{display:inline-block;background:#dc2626;color:#fff;padding:14px 28px;border-radius:6px;text-decoration:none;font-weight:700;font-size:15px}}
  .footer{{text-align:center;padding:16px;font-size:11px;color:#9ca3af}}
</style></head>
<body><div class=""card"">
  <div class=""header""><h1>⚡ Flash İndirim Başladı!</h1></div>
  <div class=""body"">
    <p>Merhaba <strong>{customerName}</strong>,</p>
    <p>Favorilerinize eklediğiniz <strong>{productName}</strong> ürününde sınırlı süreli flash indirim başladı!</p>
    <div class=""price-box"">
      <p style=""font-size:13px;color:#6b7280;margin:0 0 6px"">Normal Fiyat: <s>{originalPrice:N0} ₺</s></p>
      <p style=""font-size:34px;font-weight:900;color:#dc2626;margin:0"">%{discount} İndirim</p>
      <p style=""font-size:28px;font-weight:800;color:#1B3A6B;margin:4px 0 0"">{flashPrice:N0} ₺</p>
    </div>
    <p style=""background:#fef9c3;border-left:3px solid #eab308;padding:10px 14px;border-radius:4px;font-size:13px"">
      ⏰ Bu fırsat <strong>{endsAt}</strong> tarihine kadar geçerlidir!
    </p>
    <p style=""text-align:center;margin-top:24px"">
      <a href=""{siteUrl}/urunler/{slug}"" class=""btn"">⚡ Hemen Satın Al</a>
    </p>
  </div>
  <div class=""footer"">Adalya Solar Enerji · adalyasolar.com</div>
</div></body></html>";
    }
}

public record ReviewReplyDto(string Reply);
public record FlashSaleNotifyDto(decimal FlashPrice, decimal OriginalPrice, DateTime EndsAt);
public record AdminNoteDto(string? Note);

public class BulkProductUpdateDto
{
    public List<int> Ids { get; set; } = new();
    public decimal? Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public bool? ClearDiscount { get; set; }
    public int? Stock { get; set; }
    public bool? IsFeatured { get; set; }
    public bool? IsNew { get; set; }
}

public class CouponDto
{
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "percentage";
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int MaxUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? CampaignId { get; set; }
}
