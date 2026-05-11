using FlowMeet.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260503000000_AddNotificationDispatchedAt")]
    public partial class AddNotificationDispatchedAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispatchedAt",
                table: "Notifications");
        }
    }
}
