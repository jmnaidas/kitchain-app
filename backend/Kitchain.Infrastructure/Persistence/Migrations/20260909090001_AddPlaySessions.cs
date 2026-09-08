using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaySessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    NumberOfCourts = table.Column<int>(type: "integer", nullable: false),
                    MaximumPlayers = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RotationMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ScoringMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GameTo = table.Column<int>(type: "integer", nullable: false),
                    WinBy = table.Column<int>(type: "integer", nullable: false),
                    NextQueueOrder = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessions", x => x.Id);
                    table.CheckConstraint("CK_PlaySessions_Capacity", "\"NumberOfCourts\" > 0 AND (\"MaximumPlayers\" IS NULL OR \"MaximumPlayers\" > 0)");
                    table.CheckConstraint("CK_PlaySessions_DefaultModes", "\"RotationMode\" = 'FairRotation' AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");
                    table.CheckConstraint("CK_PlaySessions_JoinCode", "\"JoinCode\" ~ '^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$'");
                    table.CheckConstraint("CK_PlaySessions_Name", "length(trim(\"Name\")) > 0");
                    table.CheckConstraint("CK_PlaySessions_QueueCounter", "\"NextQueueOrder\" >= 0");
                    table.CheckConstraint("CK_PlaySessions_Schedule", "\"EndTime\" > \"StartTime\"");
                    table.CheckConstraint("CK_PlaySessions_Status", "\"Status\" IN ('Draft', 'Active', 'Ended')");
                    table.CheckConstraint("CK_PlaySessions_Timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "PlaySessionPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdentityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QueueOrder = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaySessionPlayers", x => x.Id);
                    table.CheckConstraint("CK_PlaySessionPlayers_Identity", "\"IdentityType\" = 'Guest'");
                    table.CheckConstraint("CK_PlaySessionPlayers_Name", "length(trim(\"DisplayName\")) > 0 AND length(trim(\"NormalizedDisplayName\")) > 0");
                    table.CheckConstraint("CK_PlaySessionPlayers_Queue", "(\"State\" = 'Waiting' AND \"QueueOrder\" IS NOT NULL AND \"QueueOrder\" > 0) OR (\"State\" IN ('Playing', 'Resting') AND \"QueueOrder\" IS NULL)");
                    table.CheckConstraint("CK_PlaySessionPlayers_State", "\"State\" IN ('Waiting', 'Playing', 'Resting')");
                    table.CheckConstraint("CK_PlaySessionPlayers_Timestamps", "\"UpdatedAt\" >= \"JoinedAt\"");
                    table.ForeignKey(
                        name: "FK_PlaySessionPlayers_PlaySessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PlaySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionPlayers_SessionId_NormalizedDisplayName",
                table: "PlaySessionPlayers",
                columns: new[] { "SessionId", "NormalizedDisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionPlayers_SessionId_QueueOrder",
                table: "PlaySessionPlayers",
                columns: new[] { "SessionId", "QueueOrder" },
                unique: true,
                filter: "\"QueueOrder\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessionPlayers_SessionId_State_QueueOrder",
                table: "PlaySessionPlayers",
                columns: new[] { "SessionId", "State", "QueueOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaySessions_JoinCode",
                table: "PlaySessions",
                column: "JoinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaySessionPlayers");

            migrationBuilder.DropTable(
                name: "PlaySessions");
        }
    }
}
