using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibes.API.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPlansTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlanDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PlanText = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPlans_VibesUser_UserId",
                        column: x => x.UserId,
                        principalTable: "VibesUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlans_UserId",
                table: "DailyPlans",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPlans");
        }
    }
}
