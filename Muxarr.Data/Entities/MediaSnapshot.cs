namespace Muxarr.Data.Entities;

/// <summary>
/// Wraps track data with file-level metadata.
/// Stored as JSON on MediaConversion for before/after/target state.
/// </summary>
public class MediaSnapshot
{
    public List<TrackSnapshot> Tracks { get; set; } = [];
    public bool HasChapters { get; set; }
    public bool HasAttachments { get; set; }
}
