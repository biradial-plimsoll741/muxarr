using System.Collections.Concurrent;
using Muxarr.Core.Config;
using Muxarr.Core.Extensions;
using Muxarr.Data;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;
using Muxarr.Web.Services.Notifications.Providers;
using Microsoft.EntityFrameworkCore;

namespace Muxarr.Web.Services.Notifications;

public static class NotificationRegistration
{
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddSingleton<INotificationProvider, PushoverProvider>();
        services.AddSingleton<INotificationProvider, DiscordProvider>();
        services.AddSingleton<INotificationProvider, GotifyProvider>();
        services.AddSingleton<INotificationProvider, NtfyProvider>();
        services.AddSingleton<INotificationProvider, WebhookProvider>();
        services.AddSingleton<NotificationService>();
        return services;
    }
}

public class NotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<NotificationProvider, INotificationProvider> _providers;
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<int, ConversionState> _lastNotifiedState = new();

    public NotificationService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IEnumerable<INotificationProvider> providers,
        MediaConverterService converter,
        ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClientFactory.CreateClient();
        _providers = providers.ToDictionary(p => p.Type);
        _logger = logger;

        converter.ConverterStateChanged += OnConverterStateChanged;
    }

    private void OnConverterStateChanged(object? sender, ConverterProgressEvent e)
    {
        var conversion = e.Conversion;
        var eventType = conversion.State switch
        {
            ConversionState.Processing => NotificationEventType.Started,
            ConversionState.Completed => NotificationEventType.Completed,
            ConversionState.Failed => NotificationEventType.Failed,
            _ => (NotificationEventType?)null
        };

        if (eventType == null)
        {
            return;
        }

        if (_lastNotifiedState.TryGetValue(conversion.Id, out var prev) && prev == conversion.State)
        {
            return;
        }

        _lastNotifiedState[conversion.Id] = conversion.State;

        if (conversion.State is ConversionState.Completed or ConversionState.Failed)
        {
            _lastNotifiedState.TryRemove(conversion.Id, out _);
        }

        _ = SendAsync(eventType.Value, conversion);
    }

    public async Task SendAsync(NotificationEventType eventType, MediaConversion conversion)
    {
        List<NotificationConfig> configs;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContext();
            configs = context.Configs.GetOrDefault<List<NotificationConfig>>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load notification configs");
            return;
        }

        var (title, body) = BuildMessage(eventType, conversion);

        foreach (var config in configs.Where(c => c.Enabled && c.HasTrigger(eventType)))
        {
            if (!_providers.TryGetValue(config.Provider, out var provider))
            {
                continue;
            }

            try
            {
                await provider.SendAsync(_httpClient, config, title, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notification '{Name}' ({Provider}) failed", config.Name, config.Provider);
            }
        }
    }

    public async Task<string?> SendTestAsync(NotificationConfig config)
    {
        if (!_providers.TryGetValue(config.Provider, out var provider))
        {
            return $"Unknown provider: {config.Provider}";
        }

        try
        {
            await provider.SendAsync(_httpClient, config, "Muxarr Test", "If you see this, notifications are working!");
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static (string Title, string Body) BuildMessage(NotificationEventType type, MediaConversion conversion)
    {
        return type switch
        {
            NotificationEventType.Started =>
                ("Conversion Started", $"{conversion.Name} - {conversion.SizeBefore.DisplayFileSize()}"),
            NotificationEventType.Completed =>
                ("Conversion Completed",
                    $"{conversion.Name} - saved {conversion.SizeDifference.DisplayFileSize()} ({conversion.GetSizeChangePercentage()})"),
            NotificationEventType.Failed =>
                ("Conversion Failed", $"{conversion.Name} - {GetLastError(conversion)}"),
            _ => ("Muxarr", conversion.Name)
        };
    }

    private static string GetLastError(MediaConversion conversion)
    {
        if (string.IsNullOrEmpty(conversion.Log))
        {
            return "Unknown error";
        }

        var lines = conversion.Log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? lines[^1].TrimStart('[').Substring(lines[^1].IndexOf(']') + 1).Trim() : "Unknown error";
    }
}
