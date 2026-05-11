using System;
using FlowMeet.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260504000000_AddBaseScheduleOccurrenceExceptions")]
    public partial class AddBaseScheduleOccurrenceExceptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BaseScheduleOccurrenceExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseScheduleEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OverrideEventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseScheduleOccurrenceExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaseScheduleOccurrenceExceptions_BaseScheduleEntries_BaseSche~",
                        column: x => x.BaseScheduleEntryId,
                        principalTable: "BaseScheduleEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseScheduleOccurrenceExceptions_Events_OverrideEventId",
                        column: x => x.OverrideEventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BaseScheduleOccurrenceExceptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseScheduleOccurrenceExceptions_BaseScheduleEntryId_Date",
                table: "BaseScheduleOccurrenceExceptions",
                columns: new[] { "BaseScheduleEntryId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaseScheduleOccurrenceExceptions_OverrideEventId",
                table: "BaseScheduleOccurrenceExceptions",
                column: "OverrideEventId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseScheduleOccurrenceExceptions_UserId",
                table: "BaseScheduleOccurrenceExceptions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseScheduleOccurrenceExceptions");
        }
    }
}
