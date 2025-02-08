using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace audiarr.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAlbumUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Albums_Name_ArtistId",
                table: "Albums");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Albums_Name_ArtistId",
                table: "Albums",
                columns: new[] { "Name", "ArtistId" },
                unique: true);
        }
    }
}
