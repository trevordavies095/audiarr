using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumGenreManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbumGenres",
                columns: table => new
                {
                    AlbumId = table.Column<string>(type: "TEXT", nullable: false),
                    GenreId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumGenres", x => new { x.AlbumId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_AlbumGenres_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumGenres_AlbumId",
                table: "AlbumGenres",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumGenres_GenreId",
                table: "AlbumGenres",
                column: "GenreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumGenres");
        }
    }
}
