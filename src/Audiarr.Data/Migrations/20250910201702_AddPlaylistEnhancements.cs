using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "PlaylistTracks",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionFloat",
                table: "PlaylistTracks",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "Playlists",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "PlayCount",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalDuration",
                table: "Playlists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackCount",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTracks_AddedAt",
                table: "PlaylistTracks",
                column: "AddedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTracks_PlaylistId_PositionFloat",
                table: "PlaylistTracks",
                columns: new[] { "PlaylistId", "PositionFloat" });

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_IsPublic",
                table: "Playlists",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_LastModified",
                table: "Playlists",
                column: "LastModified");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_UserId_IsPublic",
                table: "Playlists",
                columns: new[] { "UserId", "IsPublic" });

            // Data migration: Set LastModified to UpdatedAt for existing playlists
            migrationBuilder.Sql(
                @"UPDATE Playlists 
                  SET LastModified = UpdatedAt 
                  WHERE LastModified = '0001-01-01 00:00:00'");

            // Data migration: Set PositionFloat equal to Position for existing PlaylistTracks
            migrationBuilder.Sql(
                @"UPDATE PlaylistTracks 
                  SET PositionFloat = CAST(Position AS REAL) 
                  WHERE PositionFloat = 0");

            // Data migration: Calculate TrackCount for existing playlists
            migrationBuilder.Sql(
                @"UPDATE Playlists 
                  SET TrackCount = (
                      SELECT COUNT(*) 
                      FROM PlaylistTracks 
                      WHERE PlaylistTracks.PlaylistId = Playlists.Id
                  )");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaylistTracks_AddedAt",
                table: "PlaylistTracks");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistTracks_PlaylistId_PositionFloat",
                table: "PlaylistTracks");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_IsPublic",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_LastModified",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_UserId_IsPublic",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "PositionFloat",
                table: "PlaylistTracks");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "PlayCount",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "TotalDuration",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "TrackCount",
                table: "Playlists");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                table: "PlaylistTracks",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
