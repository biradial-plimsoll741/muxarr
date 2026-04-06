using System.Net.Http.Json;
using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications.Providers;

public class WebhookProvider : INotificationProvider
{
    public NotificationProvider Type => NotificationProvider.Webhook;

    public async Task SendAsync(HttpClient client, NotificationConfig config, string title, string body)
    {
        await client.PostAsJsonAsync(config.Url, new { title, body });
    }
}
