using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muxarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaInfoSurrogateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support altering primary keys in-place, and the old composite key
            // (Id, IsMovie) allows duplicate Id values across movie/series rows. We need to
            // rebuild the table to introduce a surrogate key and move the old Id to ExternalId.
            migrationBuilder.Sql("""
                CREATE TABLE MediaInfo_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExternalId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    OriginalLanguage TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    IsMovie INTEGER NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    UpdatedDate TEXT NOT NULL
                );

                INSERT INTO MediaInfo_new (ExternalId, Title, OriginalLanguage, Path, IsMovie, CreatedDate, UpdatedDate)
                SELECT Id, Title, OriginalLanguage, Path, IsMovie, CreatedDate, UpdatedDate FROM MediaInfo;

                DROP TABLE MediaInfo;
                ALTER TABLE MediaInfo_new RENAME TO MediaInfo;

                CREATE UNIQUE INDEX IX_MediaInfo_ExternalId_IsMovie ON MediaInfo (ExternalId, IsMovie);
                CREATE INDEX IX_MediaInfo_Path ON MediaInfo (Path);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE MediaInfo_old (
                    Id INTEGER NOT NULL,
                    IsMovie INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    OriginalLanguage TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL,
                    UpdatedDate TEXT NOT NULL,
                    PRIMARY KEY (Id, IsMovie)
                );

                INSERT INTO MediaInfo_old (Id, IsMovie, Title, OriginalLanguage, Path, CreatedDate, UpdatedDate)
                SELECT ExternalId, IsMovie, Title, OriginalLanguage, Path, CreatedDate, UpdatedDate FROM MediaInfo;

                DROP TABLE MediaInfo;
                ALTER TABLE MediaInfo_old RENAME TO MediaInfo;

                CREATE INDEX IX_MediaInfo_Path ON MediaInfo (Path);
                """);
        }
    }
}
