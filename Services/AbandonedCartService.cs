using AdalyaSolarAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AdalyaSolarAPI.Services;

public class AbandonedCartService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbandonedCartService> _logger;

    public AbandonedCartService(IServiceScopeFactory scopeFactory, ILogger<AbandonedCartService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            try { await SendReminderEmails(); } catch (Exception ex) { _logger.LogError(ex, "Terk edilen sepet e-postası hatası"); }
        }
    }

    private async Task SendReminderEmails()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<EmailService>();

        var cutoff = DateTime.UtcNow.AddHours(-2);
        var carts = await db.AbandonedCarts
            .Include(c => c.User)
            .Where(c => c.UpdatedAt <= cutoff && c.EmailSentAt == null)
            .ToListAsync();

        foreach (var cart in carts)
        {
            var subject = "Sepetinizde ürünler sizi bekliyor!";
            var body = $"""
Merhaba {cart.User.Name},

Sepetinizde tamamlanmayı bekleyen {cart.TotalAmount:N2} ₺ değerinde ürün bulunuyor.

Alışverişinizi tamamlamak için sitemizi ziyaret edin:
https://adalyasolar.com/sepet

Stoklar sınırlı olabilir, fırsatı kaçırmayın!

Adalya Solar Enerji
""";
            await email.SendAsync(cart.User.Email, cart.User.Name, subject, body);
            cart.EmailSentAt = DateTime.UtcNow;
        }

        if (carts.Count > 0) await db.SaveChangesAsync();
    }
}
