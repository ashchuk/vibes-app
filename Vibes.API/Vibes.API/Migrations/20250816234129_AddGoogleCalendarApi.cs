using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vibes.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleCalendarRefreshToken",
                table: "VibesUser",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleCalendarRefreshToken",
                table: "VibesUser");
        }
    }
}
