using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace audiarr.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_Title_AlbumId_ArtistId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Artists_Name",
                table: "Artists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Title_AlbumId_ArtistId",
                table: "Tracks",
                columns: new[] { "Title", "AlbumId", "ArtistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artists_Name",
                table: "Artists",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
