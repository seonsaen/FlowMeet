using System;
using FlowMeet.Server.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260505010000_AddEmailVerificationMeetingManagement")]
    public partial class AddEmailVerificationMeetingManagement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedGroupId",
                table: "Meetings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailVerificationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    PendingPasswordHash = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_Email_Purpose_CreatedAt",
                table: "EmailVerificationCodes",
                columns: new[] { "Email", "Purpose", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_UserId",
                table: "EmailVerificationCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RelatedGroupId",
                table: "Meetings",
                column: "RelatedGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_Groups_RelatedGroupId",
                table: "Meetings",
                column: "RelatedGroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_Groups_RelatedGroupId",
                table: "Meetings");

            migrationBuilder.DropTable(
                name: "EmailVerificationCodes");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_RelatedGroupId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "RelatedGroupId",
                table: "Meetings");
        }
    }
}
