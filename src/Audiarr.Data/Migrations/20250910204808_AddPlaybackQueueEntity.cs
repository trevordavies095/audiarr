using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Audiarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackQueueEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybackQueues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    QueueStateJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    CurrentIndex = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CurrentTrackId = table.Column<string>(type: "TEXT", nullable: true),
                    RepeatMode = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsShuffled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastActivity = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackQueues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackQueues_Tracks_CurrentTrackId",
                        column: x => x.CurrentTrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PlaybackQueues_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueues_CurrentTrackId",
                table: "PlaybackQueues",
                column: "CurrentTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueues_LastActivity",
                table: "PlaybackQueues",
                column: "LastActivity");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackQueues_UserId",
                table: "PlaybackQueues",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybackQueues");
        }
    }
}
