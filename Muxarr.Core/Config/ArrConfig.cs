using System.Text.Json.Serialization;

namespace Muxarr.Core.Config;

public class ArrConfig
{
    [JsonIgnore]
    public const string SonarrKey = "Sonarr";
    [JsonIgnore]
    public const string RadarrKey = "Radarr";

    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class ArrConfigs
{
    public ArrConfig Sonarr { get; set; } = new();
    public ArrConfig Radarr { get; set; } = new();
}