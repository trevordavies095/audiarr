using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace audiarr.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks",
                newName: "idx_tracks_album");

            migrationBuilder.CreateIndex(
                name: "idx_tracks_artist",
                table: "Tracks",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "idx_tracks_filename",
                table: "Tracks",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "idx_tracks_stream",
                table: "Tracks",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "idx_tracks_title",
                table: "Tracks",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "idx_artists_name",
                table: "Artists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "idx_artists_sortname",
                table: "Artists",
                column: "SortName");

            migrationBuilder.CreateIndex(
                name: "idx_albums_name_artist",
                table: "Albums",
                columns: new[] { "Name", "ArtistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_albums_releaseyear",
                table: "Albums",
                column: "ReleaseYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_tracks_artist",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "idx_tracks_filename",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "idx_tracks_stream",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "idx_tracks_title",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "idx_artists_name",
                table: "Artists");

            migrationBuilder.DropIndex(
                name: "idx_artists_sortname",
                table: "Artists");

            migrationBuilder.DropIndex(
                name: "idx_albums_name_artist",
                table: "Albums");

            migrationBuilder.DropIndex(
                name: "idx_albums_releaseyear",
                table: "Albums");

            migrationBuilder.RenameIndex(
                name: "idx_tracks_album",
                table: "Tracks",
                newName: "IX_Tracks_AlbumId");
        }
    }
}
