using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScannerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameNormalized",
                table: "Artists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseYear",
                table: "Albums",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleNormalized",
                table: "Albums",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "NameNormalized",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "ReleaseYear",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "TitleNormalized",
                table: "Albums");
        }
    }
}
