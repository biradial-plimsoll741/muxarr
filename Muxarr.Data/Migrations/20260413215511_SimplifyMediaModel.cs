using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyMediaModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TrackName",
                table: "MediaTrack",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "ChapterCount",
                table: "MediaFile",
                newName: "HasChapters");

            migrationBuilder.RenameColumn(
                name: "AttachmentCount",
                table: "MediaFile",
                newName: "HasAttachments");

            // Rewrite "TrackName" → "Name" inside the JSON snapshot columns so
            // existing MediaConversion rows deserialize against the renamed property.
            migrationBuilder.Sql("""
                UPDATE "MediaConversion"
                SET "SnapshotBefore" = REPLACE("SnapshotBefore", '"TrackName":', '"Name":'),
                    "SnapshotAfter"  = REPLACE("SnapshotAfter",  '"TrackName":', '"Name":');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "MediaConversion"
                SET "SnapshotBefore" = REPLACE("SnapshotBefore", '"Name":', '"TrackName":'),
                    "SnapshotAfter"  = REPLACE("SnapshotAfter",  '"Name":', '"TrackName":');
                """);

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "MediaTrack",
                newName: "TrackName");

            migrationBuilder.RenameColumn(
                name: "HasChapters",
                table: "MediaFile",
                newName: "ChapterCount");

            migrationBuilder.RenameColumn(
                name: "HasAttachments",
                table: "MediaFile",
                newName: "AttachmentCount");
        }
    }
}
