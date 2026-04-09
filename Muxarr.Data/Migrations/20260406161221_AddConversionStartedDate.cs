using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversionStartedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedDate",
                table: "MediaConversion",
                type: "TEXT",
                nullable: true);

            // Best-effort backfill: extract the first log timestamp [YYYY-MM-DD HH:MM:SS.FFF]
            // which is written when HandleConversion starts processing the file.
            // datetime() returns NULL for invalid strings, so bad data is filtered out.
            // Range check ensures the parsed date falls between queued and last updated.
            migrationBuilder.Sql("""
                UPDATE MediaConversion
                SET StartedDate = substr(Log, 2, 23)
                WHERE StartedDate IS NULL
                  AND State IN ('Completed', 'Failed', 'Processing')
                  AND Log IS NOT NULL
                  AND length(Log) > 24
                  AND substr(Log, 1, 1) = '['
                  AND datetime(substr(Log, 2, 23)) IS NOT NULL
                  AND datetime(substr(Log, 2, 23)) >= datetime(CreatedDate)
                  AND datetime(substr(Log, 2, 23)) <= datetime(UpdatedDate)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedDate",
                table: "MediaConversion");
        }
    }
}
