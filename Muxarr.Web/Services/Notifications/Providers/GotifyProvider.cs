using System.Net.Http.Json;
using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications.Providers;

public class GotifyProvider : INotificationProvider
{
    public NotificationProvider Type => NotificationProvider.Gotify;

    public async Task SendAsync(HttpClient client, NotificationConfig config, string title, string body)
    {
        var url = $"{config.Url.TrimEnd('/')}/message";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Gotify-Key", config.Token);
        request.Content = JsonContent.Create(new { title, message = body, priority = 5 });
        await client.SendAsync(request);
    }
}
