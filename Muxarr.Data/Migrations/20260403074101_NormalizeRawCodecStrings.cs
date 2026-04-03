using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    /// <summary>
    /// Normalizes raw mkvmerge/ffprobe codec strings that the MigrateCodecToEnum migration missed.
    /// That migration only converted display names (e.g. "PGS" -> "Pgs"), but raw tool strings
    /// like "HDMV PGS" could still be present in the DB, causing duplicate labels in stats.
    /// </summary>
    public partial class NormalizeRawCodecStrings : Migration
    {
        // Raw tool strings -> enum name. COLLATE NOCASE handles case variants,
        // so only one entry per distinct string (ignoring case) is needed.
        private static readonly (string Raw, string EnumName)[] RawCodecMappings =
        [
            // Subtitle - mkvmerge raw strings
            ("SubRip/SRT", "Srt"),
            ("SubRip", "Srt"),
            ("SubStationAlpha", "Ass"),
            ("SubStationAlphaASS", "Ass"),
            ("SSA", "Ass"),
            ("ASS", "Ass"),
            ("HDMV PGS", "Pgs"),
            ("HDMVPGS", "Pgs"),
            ("DVD_SUBTITLE", "VobSub"),
            ("DVDSUB", "VobSub"),
            ("TTML", "TimedText"),
            ("TIMEDTEXT", "TimedText"),
            ("TX3G", "MovText"),
            ("MOVTEXT", "MovText"),
            ("DVBSUBTITLE", "DvbSubtitle"),
            ("DVBTELETEXT", "DvbTeletext"),

            // Subtitle - ffprobe strings (distinct from above after NOCASE)
            ("hdmv_pgs_subtitle", "Pgs"),
            ("dvb_subtitle", "DvbSubtitle"),
            ("dvb_teletext", "DvbTeletext"),
            ("mov_text", "MovText"),
            ("webvtt", "WebVtt"),

            // Audio - raw tool strings and ffprobe lowercase variants.
            // COLLATE NOCASE means ("AAC", "Aac") also catches ffprobe "aac".
            ("AAC", "Aac"),
            ("AC3", "Ac3"),
            ("EAC3", "Eac3"),
            ("EAC-3", "Eac3"),
            ("DTS", "Dts"),
            ("TRUEHD", "TrueHd"),
            ("DTS-HD", "DtsHdMa"),
            ("DTS-HD MA", "DtsHdMa"),
            ("DTSHD", "DtsHdMa"),
            ("FLAC", "Flac"),
            ("Opus", "Opus"),
            ("Vorbis", "Vorbis"),
            ("MP3", "Mp3"),
            ("MPEG AUDIO", "Mp3"),

            // Video - ffprobe lowercase (HEVC/AVC handled by LIKE patterns below)
            ("AV1", "Av1"),
            ("VP9", "Vp9"),
            ("VP8", "Vp8"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Case-insensitive exact matches for known raw strings.
            // COLLATE NOCASE ensures we catch all case variants (e.g. "hdmv pgs", "HDMV PGS", "Hdmv Pgs").
            foreach (var (raw, enumName) in RawCodecMappings)
            {
                var escapedRaw = raw.Replace("'", "''");
                migrationBuilder.Sql(
                    $"UPDATE MediaTrack SET Codec = '{enumName}' WHERE Codec = '{escapedRaw}' COLLATE NOCASE AND Codec != '{enumName}';");
            }

            // Video codecs with multi-part mkvmerge strings (e.g. "HEVC/H.265/MPEG-H Part2/x265").
            // LIKE is case-insensitive in SQLite by default.
            migrationBuilder.Sql(
                "UPDATE MediaTrack SET Codec = 'Hevc' WHERE Codec != 'Hevc' AND (Codec LIKE '%HEVC%' OR Codec LIKE '%H.265%' OR Codec LIKE '%H265%');");
            migrationBuilder.Sql(
                "UPDATE MediaTrack SET Codec = 'Avc' WHERE Codec != 'Avc' AND (Codec LIKE '%AVC%' OR Codec LIKE '%H.264%' OR Codec LIKE '%H264%');");

            // PCM variants (e.g. pcm_s16le, pcm_s24le, PCM_F32LE).
            migrationBuilder.Sql(
                "UPDATE MediaTrack SET Codec = 'Pcm' WHERE Codec != 'Pcm' AND Codec LIKE 'pcm%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible - we don't know which raw variant each row originally had.
        }
    }
}
