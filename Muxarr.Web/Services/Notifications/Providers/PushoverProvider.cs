using System.Net.Http.Json;
using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications.Providers;

public class PushoverProvider : INotificationProvider
{
    public NotificationProvider Type => NotificationProvider.Pushover;

    public async Task SendAsync(HttpClient client, NotificationConfig config, string title, string body)
    {
        await client.PostAsJsonAsync("https://api.pushover.net/1/messages.json", new
        {
            token = config.AppToken,
            user = config.UserKey,
            title,
            message = body
        });
    }
}
