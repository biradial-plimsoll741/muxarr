using Muxarr.Core.Config;

namespace Muxarr.Web.Services.Notifications;

public interface INotificationProvider
{
    NotificationProvider Type { get; }
    Task SendAsync(HttpClient client, NotificationConfig config, string title, string body);
}
