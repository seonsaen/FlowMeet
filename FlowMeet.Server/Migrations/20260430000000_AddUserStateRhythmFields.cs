using FlowMeet.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260430000000_AddUserStateRhythmFields")]
    public partial class AddUserStateRhythmFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackgroundLoadLevel",
                table: "UserStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RawBalance",
                table: "UserStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SleepQuality",
                table: "UserStates",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundLoadLevel",
                table: "UserStates");

            migrationBuilder.DropColumn(
                name: "RawBalance",
                table: "UserStates");

            migrationBuilder.DropColumn(
                name: "SleepQuality",
                table: "UserStates");
        }
    }
}
