using Muxarr.Core.Models;

namespace Muxarr.Data.Entities;

/// <summary>
/// Wraps track data with file-level metadata.
/// Stored as JSON on MediaConversion for before/after/target state.
/// </summary>
public class MediaSnapshot : IMedia<TrackSnapshot>
{
    public List<TrackSnapshot> Tracks { get; set; } = [];
    public bool HasChapters { get; set; }
    public bool HasAttachments { get; set; }
}


/// <summary>
/// Immutable snapshot of track data for conversion history (stored as JSON on MediaConversion).
/// </summary>
public class TrackSnapshot : IMediaTrack
{
    public int Index { get; set; }
    public MediaTrackType Type { get; set; }
    public bool IsCommentary { get; set; }
    public bool IsHearingImpaired { get; set; }
    public bool IsVisualImpaired { get; set; }
    public bool IsDefault { get; set; }
    public bool IsForced { get; set; }
    public bool IsOriginal { get; set; }
    public bool IsDub { get; set; }
    public string Codec { get; set; } = string.Empty;
    public int AudioChannels { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string? Name { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}
