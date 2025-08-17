using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibes.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEventRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnboardingCompleted",
                table: "VibesUser",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEveningCheckupSentUtc",
                table: "VibesUser",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    GoogleEventId = table.Column<string>(type: "TEXT", nullable: false),
                    EventSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Vibe = table.Column<string>(type: "TEXT", nullable: false),
                    RatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventRatings_VibesUser_UserId",
                        column: x => x.UserId,
                        principalTable: "VibesUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventRatings_UserId",
                table: "EventRatings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRatings");

            migrationBuilder.DropColumn(
                name: "IsOnboardingCompleted",
                table: "VibesUser");

            migrationBuilder.DropColumn(
                name: "LastEveningCheckupSentUtc",
                table: "VibesUser");
        }
    }
}
