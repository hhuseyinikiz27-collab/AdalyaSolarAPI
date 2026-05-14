using System.Net;
using System.Net.Mail;
using AdalyaSolarAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace AdalyaSolarAPI.Services;

public class EmailService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EmailService> _logger;

    public EmailService(AppDbContext db, ILogger<EmailService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string toName, string subject, string body)
        => SendCoreAsync(toEmail, toName, subject, body, isHtml: false);

    public Task SendHtmlAsync(string toEmail, string toName, string subject, string html)
        => SendCoreAsync(toEmail, toName, subject, html, isHtml: true);

    private async Task SendCoreAsync(string toEmail, string toName, string subject, string body, bool isHtml)
    {
        try
        {
            var settings = await _db.SiteSettings
                .Where(s => s.Key.StartsWith("smtp."))
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            if (!settings.TryGetValue("smtp.host", out var host) || string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP ayarları yapılandırılmamış, e-posta gönderilmiyor.");
                return;
            }

            _ = int.TryParse(settings.GetValueOrDefault("smtp.port", "587"), out var port);
            var user     = settings.GetValueOrDefault("smtp.user", "");
            var password = settings.GetValueOrDefault("smtp.password", "");
            var fromEmail = settings.GetValueOrDefault("smtp.fromEmail", user);
            var fromName  = settings.GetValueOrDefault("smtp.fromName", "Adalya Solar Enerji");

            using var client = new SmtpClient(host, port)
            {
                EnableSsl   = true,
                Credentials = new NetworkCredential(user, password),
            };

            var mail = new MailMessage
            {
                From       = new MailAddress(fromEmail, fromName),
                Subject    = subject,
                Body       = body,
                IsBodyHtml = isHtml,
            };
            mail.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(mail);
            _logger.LogInformation("E-posta gönderildi: {to}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-posta gönderilemedi: {to}", toEmail);
        }
    }
}
