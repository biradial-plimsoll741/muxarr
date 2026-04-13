using Muxarr.Core.MkvToolNix;
using Muxarr.Core.Models;

namespace Muxarr.Core.FFmpeg;

public static class FFmpegHelper
{
    public static string EscapeValue(string value)
    {
        return MkvToolNixHelper.EscapeValue(value);
    }

    // Null fields = no opinion (ffmpeg preserves source).
    // Uses "comment"/"hearing_impaired" per ffmpeg convention, not "commentary"/"SDH".
    public static string? BuildDispositionValue(TrackPlan trackPlan)
    {
        var parts = new List<string>();

        if (trackPlan.IsDefault != null)
        {
            parts.Add(trackPlan.IsDefault.Value ? "+default" : "-default");
        }

        if (trackPlan.IsForced != null)
        {
            parts.Add(trackPlan.IsForced.Value ? "+forced" : "-forced");
        }

        if (trackPlan.IsHearingImpaired != null)
        {
            parts.Add(trackPlan.IsHearingImpaired.Value ? "+hearing_impaired" : "-hearing_impaired");
        }

        if (trackPlan.IsVisualImpaired != null)
        {
            parts.Add(trackPlan.IsVisualImpaired.Value ? "+visual_impaired" : "-visual_impaired");
        }

        if (trackPlan.IsCommentary != null)
        {
            parts.Add(trackPlan.IsCommentary.Value ? "+comment" : "-comment");
        }

        if (trackPlan.IsOriginal != null)
        {
            parts.Add(trackPlan.IsOriginal.Value ? "+original" : "-original");
        }

        if (trackPlan.IsDub != null)
        {
            parts.Add(trackPlan.IsDub.Value ? "+dub" : "-dub");
        }

        return parts.Count == 0 ? null : string.Join("", parts);
    }
}
