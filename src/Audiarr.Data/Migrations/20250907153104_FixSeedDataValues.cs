using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedDataValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "admin",
                columns: new[] { "CreatedAt", "PasswordHash", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "$2a$11$OfpXVYD9ge7s.q0LiudGbe3AOGBaxel1f8BAGKT4pAeQEL8Hsae0m", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: "admin",
                columns: new[] { "CreatedAt", "PasswordHash", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 7, 15, 23, 47, 670, DateTimeKind.Utc).AddTicks(1560), "$2a$11$08pO5HnjRExY.05zIXeFzeT3qmZJeDOf6f4lidY0WdPDcwo16GvnG", new DateTime(2025, 9, 7, 15, 23, 47, 670, DateTimeKind.Utc).AddTicks(1800) });
        }
    }
}
