namespace Muxarr.Core.Extensions;

public enum ContainerFamily
{
    Unknown,
    Matroska,
    Mp4
}

public static class ContainerFamilyExtensions
{
    /// <summary>
    /// Classifies a raw container type string from mkvmerge into a family the
    /// converter pipeline can dispatch on. Keeps the mkvmerge-version string
    /// variations in one place.
    /// </summary>
    public static ContainerFamily ToContainerFamily(this string? containerType)
    {
        if (string.IsNullOrEmpty(containerType))
        {
            return ContainerFamily.Unknown;
        }

        return containerType switch
        {
            // mkvmerge: Matroska, WebM (WebM is a Matroska subset)
            "Matroska" or "WebM" => ContainerFamily.Matroska,
            // mkvmerge v82 and earlier: "QuickTime/MP4"; v97+: "MP4/QuickTime"
            "QuickTime/MP4" or "MP4/QuickTime" => ContainerFamily.Mp4,
            _ => ContainerFamily.Unknown
        };
    }
}
