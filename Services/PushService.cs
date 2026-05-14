using System.Text.Json;
using AdalyaSolarAPI.Models;
using WebPush;

namespace AdalyaSolarAPI.Services;

public class PushService
{
    private readonly ILogger<PushService> _log;
    private readonly VapidDetails _vapid;

    // VAPID keys — set via env vars VAPID_PUBLIC_KEY and VAPID_PRIVATE_KEY
    // Generate once with: npx web-push generate-vapid-keys
    public string PublicKey { get; }

    public PushService(IConfiguration config, ILogger<PushService> log)
    {
        _log = log;
        // Accepts both flat env vars (VAPID_PUBLIC_KEY) and appsettings hierarchy (Vapid:PublicKey)
        PublicKey = config["VAPID_PUBLIC_KEY"]
            ?? config["Vapid:PublicKey"]
            ?? "BEl62iUYgUivxIkv69yViEuiBIa-Ib9-SkvMeAtA3LFgDzkrxZJjSgSnfckjBJuBkr3qBUYIHBQFLXYp5Nksh8U";
        var privateKey = config["VAPID_PRIVATE_KEY"]
            ?? config["Vapid:PrivateKey"]
            ?? "UUxI4O8-FbRouAevSmBQ6co62groqu5tgkFMGsEKZYw";
        var subject = config["VAPID_SUBJECT"] ?? config["Vapid:Subject"] ?? "mailto:info@adalyasolar.com";
        _vapid = new VapidDetails(subject, PublicKey, privateKey);
    }

    public async Task SendAsync(AdalyaSolarAPI.Models.PushSubscription sub, string title, string body, string url = "/", string? tag = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { title, body, url, tag = tag ?? "adalya" });
            var webPushSub = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            var client = new WebPushClient();
            await client.SendNotificationAsync(webPushSub, payload, _vapid);
        }
        catch (Exception ex)
        {
            _log.LogWarning("Push send failed for {Endpoint}: {Error}", sub.Endpoint, ex.Message);
        }
    }

    public async Task SendToAllAsync(IEnumerable<AdalyaSolarAPI.Models.PushSubscription> subs, string title, string body, string url = "/", string? tag = null)
    {
        var tasks = subs.Select(s => SendAsync(s, title, body, url, tag));
        await Task.WhenAll(tasks);
    }
}
