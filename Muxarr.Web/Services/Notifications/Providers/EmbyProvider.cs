using Muxarr.Web.Components.Shared;

namespace Muxarr.Web.Services.Notifications.Providers;

public class EmbySettings
{
    [Field("Server URL", Type = FieldType.Url, Placeholder = "http://192.168.1.10:8096",
        HelpText = "Your Emby server base URL.")]
    public string ServerUrl { get; set; } = "";

    [Field("API Key", Type = FieldType.Password,
        HelpText = "Generate one at Dashboard > Advanced > Security > Api Keys.")]
    public string ApiKey { get; set; } = "";

    [Field("Library Item ID", Placeholder = "leave blank to refresh the entire server",
        HelpText = "Library ItemId. Find via /Library/VirtualFolders.")]
    public string LibraryItemId { get; set; } = "";

    [Field("Full metadata refresh", Type = FieldType.Checkbox,
        HelpText = "Re-fetches all metadata and images instead of only what's missing. Slower; only applies when a Library Item ID is set.")]
    public bool FullMetadataRefresh { get; set; }
}

public class EmbyProvider : NotificationProvider<EmbySettings>
{
    public override string Icon => "bi-play-circle-fill";

    protected override Task SendCoreAsync(HttpClient client, EmbySettings s, NotificationPayload payload) =>
        JellyfinProvider.RefreshAsync(client, s.ServerUrl, s.ApiKey, s.LibraryItemId, s.FullMetadataRefresh);
}
