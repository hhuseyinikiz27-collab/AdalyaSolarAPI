using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using AdalyaSolarAPI.Services;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public OrdersController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var uid = GetUserId();
        var orders = await _db.Orders
            .Where(o => o.UserId == uid)
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
                o.CreatedAt,
                Items = o.Items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    i.Quantity,
                    i.UnitPrice,
                    ProductName = i.Product.Name,
                    ProductImageUrl = i.Product.ImageUrl,
                    WarrantyMonths = i.Product.WarrantyMonths,
                }).ToList(),
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        var uid = GetUserId();

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { message = "Sepet boş." });

        var currentUser = await _db.Users.FindAsync(uid);
        if (currentUser == null) return Unauthorized();

        if (currentUser.SpamBanUntil.HasValue && currentUser.SpamBanUntil > DateTime.UtcNow)
        {
            var remaining = (int)Math.Ceiling((currentUser.SpamBanUntil.Value - DateTime.UtcNow).TotalMinutes);
            return BadRequest(new { message = $"Sipariş oluşturma yetkiniz geçici olarak kısıtlandı. Kısıtlama yaklaşık {remaining} dakika sonra kalkacak." });
        }

        var settings = await _db.SiteSettings
            .Where(s => s.Key == "security.cancelSpamLimitPerHour" || s.Key == "security.cancelSpamBanMinutes" ||
                        s.Key == "security.orderSpamLimitPerHour" || s.Key == "security.orderSpamBanMinutes")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        int cancelLimit = settings.TryGetValue("security.cancelSpamLimitPerHour", out var clv) && int.TryParse(clv, out var cl) ? cl : 5;
        int cancelBanMins = settings.TryGetValue("security.cancelSpamBanMinutes", out var cbv) && int.TryParse(cbv, out var cb) ? cb : 30;
        int orderLimit = settings.TryGetValue("security.orderSpamLimitPerHour", out var olv) && int.TryParse(olv, out var ol) ? ol : 5;
        int orderBanMins = settings.TryGetValue("security.orderSpamBanMinutes", out var obv) && int.TryParse(obv, out var ob) ? ob : 30;

        var oneHourAgo = DateTime.UtcNow.AddHours(-1);

        var recentOrders = await _db.Orders
            .CountAsync(o => o.UserId == uid && o.CreatedAt >= oneHourAgo);
        if (recentOrders > orderLimit)
        {
            currentUser.SpamBanUntil = DateTime.UtcNow.AddMinutes(orderBanMins);
            await _db.SaveChangesAsync();
            return BadRequest(new { message = $"Son 1 saat içinde çok fazla sipariş oluşturdunuz. {orderBanMins} dakika boyunca yeni sipariş oluşturamazsınız." });
        }

        var recentCancellations = await _db.Orders
            .CountAsync(o => o.UserId == uid && o.Status == "iptal" && o.CreatedAt >= oneHourAgo);
        if (recentCancellations > cancelLimit)
        {
            currentUser.SpamBanUntil = DateTime.UtcNow.AddMinutes(cancelBanMins);
            await _db.SaveChangesAsync();
            return BadRequest(new { message = $"Son 1 saat içinde çok fazla sipariş iptal ettiniz. {cancelBanMins} dakika boyunca yeni sipariş oluşturamazsınız." });
        }

        var productIds = dto.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var orderItems = new List<OrderItem>();
        decimal total = 0;

        foreach (var item in dto.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product == null) return BadRequest(new { message = $"Ürün bulunamadı: {item.ProductId}" });
            if (product.Stock < item.Quantity) return BadRequest(new { message = $"{product.Name} için yeterli stok yok." });

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
            });
            total += product.Price * item.Quantity;
        }

        // Kupon uygula (ürün toplamına, kargo öncesinde)
        if (!string.IsNullOrWhiteSpace(dto.CouponCode))
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c =>
                c.Code == dto.CouponCode.Trim().ToUpper() && c.IsActive);
            if (coupon != null &&
                !(coupon.ExpiresAt.HasValue && coupon.ExpiresAt < DateTime.UtcNow) &&
                !(coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses) &&
                total >= coupon.MinOrderAmount)
            {
                if (coupon.DiscountType == "percentage")
                    total -= total * coupon.DiscountValue / 100;
                else
                    total -= Math.Min(coupon.DiscountValue, total);
                coupon.UsedCount++;
            }
        }

        // Hediye kartı uygula
        GiftCard? usedGiftCard = null;
        decimal giftCardApplied = 0;
        if (!string.IsNullOrWhiteSpace(dto.GiftCardCode))
        {
            var gc = await _db.GiftCards.FirstOrDefaultAsync(g =>
                g.Code == dto.GiftCardCode.Trim().ToUpper() && g.IsActive && g.Balance > 0);
            if (gc != null && !(gc.ExpiresAt.HasValue && gc.ExpiresAt < DateTime.UtcNow))
            {
                giftCardApplied = Math.Min(gc.Balance, total);
                gc.Balance -= giftCardApplied;
                if (gc.Balance == 0) gc.IsActive = false;
                total -= giftCardApplied;
                usedGiftCard = gc;
            }
        }

        // Kargo
        var shippingSettings = await _db.SiteSettings
            .Where(s => s.Key == "shipping.cost" || s.Key == "shipping.freeAbove")
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        decimal shippingCost  = decimal.TryParse(shippingSettings.GetValueOrDefault("shipping.cost",      "99"),  out var sc) ? sc : 99m;
        decimal shippingFree  = decimal.TryParse(shippingSettings.GetValueOrDefault("shipping.freeAbove", "500"), out var sf) ? sf : 500m;
        decimal shipping = total >= shippingFree ? 0 : shippingCost;
        total += shipping;

        var order = new Order
        {
            UserId = uid,
            Items = orderItems,
            Total = total,
            ShippingFullName = dto.ShippingFullName,
            ShippingPhone = dto.ShippingPhone,
            ShippingAddress = dto.ShippingAddress,
            Note = dto.Note ?? string.Empty,
        };

        _db.Orders.Add(order);

        // Stok düş
        var lowStockProducts = new List<Product>();
        foreach (var item in dto.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            product.Stock -= item.Quantity;
            if (product.Stock <= 5)
                lowStockProducts.Add(product);
        }

        await _db.SaveChangesAsync();

        // Düşük stok admin bildirimi
        if (lowStockProducts.Count > 0)
        {
            var admins = await _db.Users.Where(u => u.Role == "admin").ToListAsync();
            foreach (var admin in admins)
                foreach (var lsp in lowStockProducts)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        Title = "Düşük Stok Uyarısı",
                        Message = $"'{lsp.Name}' ürününde yalnızca {lsp.Stock} adet kaldı!",
                        Type = "admin",
                    });
                    if (lsp.Stock == 0)
                        _ = _email.SendAsync(admin.Email, admin.Name, "Stok Tükendi: " + lsp.Name,
                            $"'{lsp.Name}' ürünü tamamen tükendi. Stok güncellemesi gerekiyor.");
                }
            await _db.SaveChangesAsync();
        }

        _db.Notifications.Add(new Notification
        {
            UserId = uid,
            Title = "Siparişiniz Alındı",
            Message = $"#{order.Id} numaralı siparişiniz başarıyla oluşturuldu. Toplam tutar: {order.Total:N2} ₺",
            Type = "order",
        });

        // Terk edilen sepeti temizle
        var abandonedCart = await _db.AbandonedCarts.FirstOrDefaultAsync(c => c.UserId == uid);
        if (abandonedCart != null) _db.AbandonedCarts.Remove(abandonedCart);

        // Sadakat puanı kazan (her 10 TL = 1 puan)
        int earnedPoints = (int)Math.Floor(total / 10);
        if (earnedPoints > 0)
        {
            currentUser.LoyaltyPoints += earnedPoints;
            _db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                UserId = uid,
                Points = earnedPoints,
                Type = "earned",
                OrderId = order.Id,
                Description = $"Sipariş #{order.Id} için kazanıldı",
            });
            _db.Notifications.Add(new Notification
            {
                UserId = uid,
                Title = "Puan Kazandınız!",
                Message = $"#{order.Id} numaralı siparişinizden {earnedPoints} puan kazandınız. Toplam puanınız: {currentUser.LoyaltyPoints}",
                Type = "promo",
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { order.Id, order.Total, order.Status, order.CreatedAt, earnedPoints });
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var uid = GetUserId();

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);

        if (order == null) return NotFound(new { message = "Sipariş bulunamadı." });
        if (order.Status != "hazirlanıyor")
            return BadRequest(new { message = "Sadece 'Hazırlanıyor' durumundaki siparişler iptal edilebilir." });
        if ((DateTime.UtcNow - order.CreatedAt).TotalMinutes > 30)
            return BadRequest(new { message = "Sipariş oluşturulduktan sonra yalnızca 30 dakika içinde iptal edilebilir." });

        order.Status = "iptal";

        // Stokları geri ekle
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null) product.Stock += item.Quantity;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Sipariş iptal edildi." });
    }

    [HttpGet("track/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return NotFound(new { message = "Sipariş bulunamadı." });

        return Ok(new {
            order.Id, order.Status, order.Total, order.CreatedAt,
            order.ShippingFullName, order.ShippingAddress,
            order.TrackingCode, order.CargoCompany,
            Items = order.Items.Select(i => new {
                i.ProductId, i.Quantity, i.UnitPrice,
                ProductName = i.Product?.Name ?? "",
            }),
        });
    }
}

public class CreateOrderDto
{
    public List<OrderItemDto> Items { get; set; } = new();
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? CouponCode { get; set; }
    public string? GiftCardCode { get; set; }
}

public class OrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
