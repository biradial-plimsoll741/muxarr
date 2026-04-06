using System.Text;
using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications.Providers;

public class NtfyProvider : INotificationProvider
{
    public NotificationProvider Type => NotificationProvider.Ntfy;

    public async Task SendAsync(HttpClient client, NotificationConfig config, string title, string body)
    {
        var url = $"{config.Url.TrimEnd('/')}/{config.Topic}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Title", title);
        request.Content = new StringContent(body, Encoding.UTF8, "text/plain");
        await client.SendAsync(request);
    }
}
