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
            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_AlbumName_TrackNumber",
                table: "MusicTracks",
                columns: new[] { "AlbumName", "TrackNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_Artist",
                table: "MusicTracks",
                column: "Artist");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_Artist_AlbumName",
                table: "MusicTracks",
                columns: new[] { "Artist", "AlbumName" });

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_FilePath",
                table: "MusicTracks",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_Id",
                table: "MusicTracks",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_AlbumName_TrackNumber",
                table: "MusicTracks");

            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_Artist",
                table: "MusicTracks");

            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_Artist_AlbumName",
                table: "MusicTracks");

            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_FilePath",
                table: "MusicTracks");

            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_Id",
                table: "MusicTracks");
        }
    }
}
