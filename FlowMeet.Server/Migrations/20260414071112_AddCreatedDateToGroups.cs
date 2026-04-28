using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowMeet.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedDateToGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMember_Groups_GroupId",
                table: "GroupMember");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMember_Users_UserId",
                table: "GroupMember");

            migrationBuilder.DropTable(
                name: "MeetingParticipant");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupMember",
                table: "GroupMember");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TemplateDayOfWeek",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "GroupMember");

            migrationBuilder.RenameTable(
                name: "GroupMember",
                newName: "GroupMembers");

            migrationBuilder.RenameIndex(
                name: "IX_GroupMember_UserId",
                table: "GroupMembers",
                newName: "IX_GroupMembers_UserId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Groups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinDate",
                table: "GroupMembers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "GroupMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupMembers",
                table: "GroupMembers",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateTable(
                name: "BaseScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseScheduleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaseScheduleEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupInvites",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInvites", x => new { x.GroupId, x.InviteeId });
                    table.ForeignKey(
                        name: "FK_GroupInvites_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupInvites_Users_InviteeId",
                        column: x => x.InviteeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupInvites_Users_InviterId",
                        column: x => x.InviterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invites",
                columns: table => new
                {
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invites", x => new { x.MeetingId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Invites_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_OwnerId",
                table: "Groups",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BaseScheduleEntries_UserId",
                table: "BaseScheduleEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvites_InviteeId",
                table: "GroupInvites",
                column: "InviteeId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvites_InviterId",
                table: "GroupInvites",
                column: "InviterId");

            migrationBuilder.CreateIndex(
                name: "IX_Invites_UserId",
                table: "Invites",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMembers_Groups_GroupId",
                table: "GroupMembers",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMembers_Users_UserId",
                table: "GroupMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Users_OwnerId",
                table: "Groups",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMembers_Groups_GroupId",
                table: "GroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMembers_Users_UserId",
                table: "GroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Users_OwnerId",
                table: "Groups");

            migrationBuilder.DropTable(
                name: "BaseScheduleEntries");

            migrationBuilder.DropTable(
                name: "GroupInvites");

            migrationBuilder.DropTable(
                name: "Invites");

            migrationBuilder.DropIndex(
                name: "IX_Groups_OwnerId",
                table: "Groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GroupMembers",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "JoinDate",
                table: "GroupMembers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "GroupMembers");

            migrationBuilder.RenameTable(
                name: "GroupMembers",
                newName: "GroupMember");

            migrationBuilder.RenameIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMember",
                newName: "IX_GroupMember_UserId");

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TemplateDayOfWeek",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "GroupMember",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GroupMember",
                table: "GroupMember",
                columns: new[] { "GroupId", "UserId" });

            migrationBuilder.CreateTable(
                name: "MeetingParticipant",
                columns: table => new
                {
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingParticipant", x => new { x.MeetingId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MeetingParticipant_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingParticipant_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipant_UserId",
                table: "MeetingParticipant",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMember_Groups_GroupId",
                table: "GroupMember",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMember_Users_UserId",
                table: "GroupMember",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
