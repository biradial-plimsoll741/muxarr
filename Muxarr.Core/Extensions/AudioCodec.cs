using System.ComponentModel.DataAnnotations;

namespace Muxarr.Core.Extensions;

public enum AudioCodec
{
    [Display(Name = "AAC")]
    Aac,

    [Display(Name = "AC-3")]
    Ac3,

    [Display(Name = "E-AC-3")]
    Eac3,

    [Display(Name = "DTS")]
    Dts,

    [Display(Name = "DTS-HD Master Audio")]
    DtsHdMa,

    [Display(Name = "TrueHD")]
    TrueHd,

    [Display(Name = "FLAC")]
    Flac,

    [Display(Name = "Opus")]
    Opus,

    [Display(Name = "Vorbis")]
    Vorbis,

    [Display(Name = "MP3")]
    Mp3,

    [Display(Name = "PCM")]
    Pcm,

    [Display(Name = "Unknown")]
    Unknown,
}

public static class AudioCodecExtensions
{
    public static AudioCodec ParseAudioCodec(string codec)
    {
        var upper = codec.ToUpperInvariant();

        // PCM has many variants (ffprobe: pcm_s16le, pcm_s24le, pcm_f32le, etc.)
        if (upper.StartsWith("PCM"))
        {
            return AudioCodec.Pcm;
        }

        return upper switch
        {
            // mkvmerge: AAC; ffprobe: aac
            "AAC" => AudioCodec.Aac,
            // mkvmerge: AC-3; ffprobe: ac3
            "AC3" or "AC-3" => AudioCodec.Ac3,
            // mkvmerge: E-AC-3; ffprobe: eac3
            "EAC3" or "E-AC-3" or "EAC-3" => AudioCodec.Eac3,
            // mkvmerge: DTS; ffprobe: dts
            "DTS" => AudioCodec.Dts,
            // mkvmerge: DTS-HD Master Audio; ffprobe: dts (profile=DTS-HD MA)
            "DTS-HD MASTER AUDIO" or "DTSHD" or "DTS-HD" or "DTS-HD MA" => AudioCodec.DtsHdMa,
            // mkvmerge: TrueHD; ffprobe: truehd
            "TRUEHD" => AudioCodec.TrueHd,
            // mkvmerge: FLAC; ffprobe: flac
            "FLAC" => AudioCodec.Flac,
            // mkvmerge: Opus; ffprobe: opus
            "OPUS" => AudioCodec.Opus,
            // mkvmerge: Vorbis; ffprobe: vorbis
            "VORBIS" => AudioCodec.Vorbis,
            // mkvmerge: MP3; ffprobe: mp3
            "MP3" or "MPEG AUDIO" => AudioCodec.Mp3,
            _ => AudioCodec.Unknown,
        };
    }
}
