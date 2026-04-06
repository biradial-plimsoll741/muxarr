namespace Muxarr.Core.Config;

public class NotificationConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public NotificationProvider Provider { get; set; }
    public bool Enabled { get; set; } = true;

    public bool OnStarted { get; set; }
    public bool OnCompleted { get; set; } = true;
    public bool OnFailed { get; set; } = true;

    // Pushover
    public string AppToken { get; set; } = "";
    public string UserKey { get; set; } = "";

    // Discord / Gotify / Webhook
    public string Url { get; set; } = "";

    // Gotify
    public string Token { get; set; } = "";

    // ntfy
    public string Topic { get; set; } = "";

    public bool HasTrigger(NotificationEventType type) => type switch
    {
        NotificationEventType.Started => OnStarted,
        NotificationEventType.Completed => OnCompleted,
        NotificationEventType.Failed => OnFailed,
        _ => false
    };
}

public enum NotificationProvider
{
    Pushover,
    Discord,
    Gotify,
    Ntfy,
    Webhook
}

public enum NotificationEventType
{
    Started,
    Completed,
    Failed
}
