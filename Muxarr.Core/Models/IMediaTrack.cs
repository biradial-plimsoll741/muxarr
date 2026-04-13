namespace Muxarr.Core.Models;

public interface IMediaTrack
{
    int Index { get; }
    MediaTrackType Type { get; }
    string? Name { get; }
    string LanguageCode { get; }
    string LanguageName { get; }
    string Codec { get; }
    int AudioChannels { get; }
    long DurationMs { get; }
    bool IsDefault { get; }
    bool IsForced { get; }
    bool IsCommentary { get; }
    bool IsHearingImpaired { get; }
    bool IsVisualImpaired { get; }
    bool IsOriginal { get; }
    bool IsDub { get; }
}
