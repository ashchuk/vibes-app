using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibes.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLastCheckupSentColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckupSentUtc",
                table: "VibesUser",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCheckupSentUtc",
                table: "VibesUser");
        }
    }
}
