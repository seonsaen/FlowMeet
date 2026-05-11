using System;
using FlowMeet.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505000000_AddBaseScheduleEntryVersioning")]
    public partial class AddBaseScheduleEntryVersioning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveFromDate",
                table: "BaseScheduleEntries",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveToDate",
                table: "BaseScheduleEntries",
                type: "date",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectiveFromDate",
                table: "BaseScheduleEntries");

            migrationBuilder.DropColumn(
                name: "EffectiveToDate",
                table: "BaseScheduleEntries");
        }
    }
}
