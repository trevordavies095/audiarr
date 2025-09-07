using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackPlaybackProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channels",
                table: "Tracks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPlayedDate",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlayCount",
                table: "Tracks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channels",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LastPlayedDate",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "PlayCount",
                table: "Tracks");
        }
    }
}
