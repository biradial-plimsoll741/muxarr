using Muxarr.Core.Extensions;
using Muxarr.Core.MkvToolNix;
using Muxarr.Core.Models;
using Muxarr.Data.Entities;

namespace Muxarr.Data.Extensions;

// Container-specific resolution of a desired target. Run by the builders so
// the ConversionPlan they hand off is already valid for the output container.
// The planner, converters, and UI preview all read a resolved target; none
// of them need to know about quirks.
//
// Matroska has no FlagDub on any track type. When IsDub is set on an unlocked
// target track we rewrite its title to encode the dub state (TrackNameFlags
// .EncodeDubInName) and null IsDub so mkvmerge/mkvpropedit never see a flag
// they can't express.
public static class TargetResolver
{
    public static void ResolveForContainer(ConversionPlan target, MediaSnapshot source, ContainerFamily family,
        bool sourceHasFaststart = false)
    {
        // Faststart is an MP4-only concern. On Matroska it is meaningless - null it
        // out so the stored target doesn't carry a stale opinion. On MP4 resolve
        // "inherit" (null) against the source's current layout so the target leaves
        // the builder with a concrete decision.
        if (family == ContainerFamily.Mp4)
        {
            target.Faststart ??= sourceHasFaststart;
        }
        else
        {
            target.Faststart = null;
        }

        if (family != ContainerFamily.Matroska)
        {
            return;
        }

        var sourceByNumber = source.Tracks.ToDictionary(t => t.TrackNumber);

        foreach (var track in target.Tracks)
        {
            if (track.IsDub == null)
            {
                continue;
            }

            if (!track.NameLocked)
            {
                sourceByNumber.TryGetValue(track.TrackNumber, out var original);
                var effectiveName = track.Name ?? original?.TrackName;
                var encoded = TrackNameFlags.EncodeDubInName(effectiveName, track.IsDub.Value);
                if (!string.Equals(encoded ?? "", effectiveName ?? "", StringComparison.Ordinal))
                {
                    track.Name = encoded ?? "";
                }
            }

            track.IsDub = null;
        }
    }
}
