using Muxarr.Web.Components.Shared;

namespace Muxarr.Web.Services.Notifications.Providers;

public class JellyfinSettings
{
    [Field("Server URL", Type = FieldType.Url, Placeholder = "http://192.168.1.10:8096",
        HelpText = "Your Jellyfin server base URL.")]
    public string ServerUrl { get; set; } = "";

    [Field("API Key", Type = FieldType.Password,
        HelpText = "Generate one at Dashboard > API Keys.")]
    public string ApiKey { get; set; } = "";

    [Field("Library Item ID", Placeholder = "leave blank to refresh the entire server",
        HelpText = "VirtualFolder ItemId. Find via /Library/VirtualFolders.")]
    public string LibraryItemId { get; set; } = "";

    [Field("Full metadata refresh", Type = FieldType.Checkbox,
        HelpText = "Re-fetches all metadata and images instead of only what's missing. Slower; only applies when a Library Item ID is set.")]
    public bool FullMetadataRefresh { get; set; }
}

public class JellyfinProvider : NotificationProvider<JellyfinSettings>
{
    public override string Icon => "bi-collection-play";

    protected override Task SendCoreAsync(HttpClient client, JellyfinSettings s, NotificationPayload payload) =>
        RefreshAsync(client, s.ServerUrl, s.ApiKey, s.LibraryItemId, s.FullMetadataRefresh);

    /// <summary>
    /// Triggers a library rescan on Jellyfin or Emby. Both servers share the same
    /// /Library/Refresh and /Items/{id}/Refresh endpoints (Jellyfin forked from Emby in 2018)
    /// and both accept the X-Emby-Token header for API key auth, so the implementation is shared.
    /// </summary>
    public static async Task RefreshAsync(HttpClient client, string serverUrl, string apiKey,
        string libraryItemId, bool fullMetadataRefresh)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Server URL and API Key are required.");
        }

        var baseUrl = serverUrl.TrimEnd('/');
        string url;

        if (string.IsNullOrWhiteSpace(libraryItemId))
        {
            url = $"{baseUrl}/Library/Refresh";
        }
        else
        {
            var mode = fullMetadataRefresh ? "FullRefresh" : "Default";
            url = $"{baseUrl}/Items/{Uri.EscapeDataString(libraryItemId.Trim())}/Refresh"
                  + $"?metadataRefreshMode={mode}&imageRefreshMode={mode}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Emby-Token", apiKey);
        await SendRequestAsync(client, request);
    }
}
