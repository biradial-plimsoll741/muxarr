using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    // Renames the per-track positional identifier from TrackNumber to Index.
    // Matches ffprobe's "index" field and mkvmerge's positional "id" usage;
    // "TrackNumber" was misleading since in Matroska it's a distinct EBML
    // element we don't actually store.
    //
    // Three places get renamed:
    //   1. MediaTrack.TrackNumber column -> MediaTrack.Index.
    //   2. Unique index on (MediaFileId, TrackNumber) -> (MediaFileId, Index).
    //   3. JSON blobs in MediaConversion:
    //      SnapshotBefore / SnapshotAfter: TrackSnapshot's legacy "Id" key
    //        (from [JsonPropertyName("Id")]) -> "Index".
    //      ConversionPlan: TrackPlan's "TrackNumber" key -> "Index". Pre-
    //        TargetSnapshotRefactor terminal rows stored MediaSnapshot shape
    //        here with "Id" keys, so we rewrite both.
    // Key names are anchored with a trailing colon so string values containing
    // "Id" or "TrackNumber" are not touched.
    public partial class RenameTrackNumberToIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The TargetSnapshot -> ConversionPlan property rename never got its
            // own migration - the model snapshot was updated but the DB column
            // stayed as "TargetSnapshot". Rename it here before the JSON rewrite
            // references the new column name.
            migrationBuilder.RenameColumn(
                name: "TargetSnapshot",
                table: "MediaConversion",
                newName: "ConversionPlan");

            migrationBuilder.RenameColumn(
                name: "TrackNumber",
                table: "MediaTrack",
                newName: "Index");

            migrationBuilder.RenameIndex(
                name: "IX_MediaTrack_MediaFileId_TrackNumber",
                table: "MediaTrack",
                newName: "IX_MediaTrack_MediaFileId_Index");

            migrationBuilder.Sql("""
                UPDATE "MediaConversion"
                SET "SnapshotBefore" = REPLACE("SnapshotBefore", '"Id":', '"Index":'),
                    "SnapshotAfter"  = REPLACE("SnapshotAfter",  '"Id":', '"Index":'),
                    "ConversionPlan" = REPLACE(REPLACE("ConversionPlan", '"TrackNumber":', '"Index":'), '"Id":', '"Index":');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_MediaTrack_MediaFileId_Index",
                table: "MediaTrack",
                newName: "IX_MediaTrack_MediaFileId_TrackNumber");

            migrationBuilder.RenameColumn(
                name: "Index",
                table: "MediaTrack",
                newName: "TrackNumber");

            migrationBuilder.RenameColumn(
                name: "ConversionPlan",
                table: "MediaConversion",
                newName: "TargetSnapshot");

            // JSON rewrite is not reverted: we can't tell which rows were
            // rewritten by Up versus already shaped with "Index" natively.
        }
    }
}
