using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateCodecToEnum : Migration
    {
        private static readonly (string Old, string New)[] CodecMappings =
        [
            // Video
            ("H.265 / HEVC", "Hevc"),
            ("H.264 / AVC", "Avc"),
            ("AV1", "Av1"),
            ("VP9", "Vp9"),
            ("VP8", "Vp8"),
            // Audio
            ("AAC", "Aac"),
            ("AC-3", "Ac3"),
            ("E-AC-3", "Eac3"),
            ("DTS", "Dts"),
            ("DTS-HD Master Audio", "DtsHdMa"),
            ("TrueHD", "TrueHd"),
            ("FLAC", "Flac"),
            ("Opus", "Opus"),
            ("Vorbis", "Vorbis"),
            ("MP3", "Mp3"),
            ("PCM", "Pcm"),
            // Subtitle
            ("SRT", "Srt"),
            ("ASS/SSA", "Ass"),
            ("PGS", "Pgs"),
            ("VobSub", "VobSub"),
            ("Timed Text", "TimedText"),
            ("WebVTT", "WebVtt"),
            ("DVB Subtitle", "DvbSubtitle"),
            ("DVB Teletext", "DvbTeletext"),
            ("MOV Text", "MovText"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migrate MediaTrack.Codec column
            foreach (var (old, @new) in CodecMappings)
            {
                migrationBuilder.Sql(
                    $"UPDATE MediaTrack SET Codec = '{@new}' WHERE Codec = '{old}';");
            }

            // Migrate TrackSnapshot JSON inside MediaConversion columns
            foreach (var (old, @new) in CodecMappings)
            {
                var escapedOld = old.Replace("'", "''");
                foreach (var column in new[] { "TracksBefore", "TracksAfter", "AllowedTracks" })
                {
                    migrationBuilder.Sql(
                        $"UPDATE MediaConversion SET {column} = REPLACE({column}, '\"Codec\":\"{escapedOld}\"', '\"Codec\":\"{@new}\"') WHERE {column} LIKE '%\"Codec\":\"{escapedOld}\"%';");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: enum name back to display name
            foreach (var (old, @new) in CodecMappings)
            {
                migrationBuilder.Sql(
                    $"UPDATE MediaTrack SET Codec = '{old}' WHERE Codec = '{@new}';");
            }

            foreach (var (old, @new) in CodecMappings)
            {
                var escapedOld = old.Replace("'", "''");
                foreach (var column in new[] { "TracksBefore", "TracksAfter", "AllowedTracks" })
                {
                    migrationBuilder.Sql(
                        $"UPDATE MediaConversion SET {column} = REPLACE({column}, '\"Codec\":\"{@new}\"', '\"Codec\":\"{escapedOld}\"') WHERE {column} LIKE '%\"Codec\":\"{@new}\"%';");
                }
            }
        }
    }
}
