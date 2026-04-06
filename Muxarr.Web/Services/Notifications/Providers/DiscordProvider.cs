using System.Net.Http.Json;
using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications.Providers;

public class DiscordProvider : INotificationProvider
{
    public NotificationProvider Type => NotificationProvider.Discord;

    public async Task SendAsync(HttpClient client, NotificationConfig config, string title, string body)
    {
        await client.PostAsJsonAsync(config.Url, new
        {
            embeds = new[]
            {
                new { title, description = body, color = 3447003 }
            }
        });
    }
}
