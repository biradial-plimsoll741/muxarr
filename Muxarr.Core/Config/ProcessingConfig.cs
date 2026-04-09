namespace Muxarr.Core.Config;

public class ProcessingConfig
{
    /// <summary>
    /// Interval in minutes between automatic library scans.
    /// Set to 0 to disable scheduled scanning (manual scans still work).
    /// </summary>
    public int ScanIntervalMinutes { get; set; } = 1440;

    public bool PostProcessingEnabled { get; set; }
    public string PostProcessingCommand { get; set; } = string.Empty;

    public string ResolveCommand(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var filename = Path.GetFileNameWithoutExtension(filePath);

        return PostProcessingCommand
            .Replace("{{file}}", filePath)
            .Replace("{file}", filePath)
            .Replace("{{filename}}", filename)
            .Replace("{filename}", filename)
            .Replace("{{directory}}", directory)
            .Replace("{directory}", directory);
    }
}
