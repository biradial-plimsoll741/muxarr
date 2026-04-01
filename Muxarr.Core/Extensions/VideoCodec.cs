using System.ComponentModel.DataAnnotations;

namespace Muxarr.Core.Extensions;

public enum VideoCodec
{
    [Display(Name = "H.265 / HEVC")]
    Hevc,

    [Display(Name = "H.264 / AVC")]
    Avc,

    [Display(Name = "AV1")]
    Av1,

    [Display(Name = "VP9")]
    Vp9,

    [Display(Name = "VP8")]
    Vp8,

    [Display(Name = "Unknown")]
    Unknown,
}

public static class VideoCodecExtensions
{
    public static VideoCodec ParseVideoCodec(string codec)
    {
        var upper = codec.ToUpperInvariant();

        // mkvmerge uses multi-part strings like "HEVC/H.265/MPEG-H", "AVC/H.264/MPEG-4p10"
        if (upper.Contains("HEVC") || upper.Contains("H.265") || upper.Contains("H265"))
        {
            return VideoCodec.Hevc;
        }

        if (upper.Contains("AVC") || upper.Contains("H.264") || upper.Contains("H264"))
        {
            return VideoCodec.Avc;
        }

        return upper switch
        {
            // ffprobe: hevc, h264; mkvmerge: AV1, VP9, VP8
            "AV1" => VideoCodec.Av1,
            "VP9" => VideoCodec.Vp9,
            "VP8" => VideoCodec.Vp8,
            _ => VideoCodec.Unknown,
        };
    }
}
