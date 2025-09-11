using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangePositionFloatToDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "PositionFloat",
                table: "PlaylistTracks",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldPrecision: 18,
                oldScale: 6);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PositionFloat",
                table: "PlaylistTracks",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");
        }
    }
}
