using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class AddPlayMatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlayMatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourtNumber = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayMatches", x => x.Id);
                table.CheckConstraint("CK_PlayMatches_Court", "\"CourtNumber\" > 0");
                table.CheckConstraint("CK_PlayMatches_Status", "(\"Status\" = 'Active' AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"CompletedAt\" >= \"StartedAt\" AND \"CompletedAt\" IS NOT NULL)");
                table.ForeignKey("FK_PlayMatches_PlaySessions_SessionId", x => x.SessionId, "PlaySessions", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateTable(
            name: "PlayMatchPlayers",
            columns: table => new
            {
                MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Team = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayMatchPlayers", x => new { x.MatchId, x.PlayerId });
                table.CheckConstraint("CK_PlayMatchPlayers_TeamPosition", "(\"Position\" IN (1, 2) AND \"Team\" = 'A') OR (\"Position\" IN (3, 4) AND \"Team\" = 'B')");
                table.ForeignKey("FK_PlayMatchPlayers_PlayMatches_MatchId", x => x.MatchId, "PlayMatches", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_PlayMatchPlayers_PlaySessionPlayers_PlayerId", x => x.PlayerId, "PlaySessionPlayers", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_PlayMatches_SessionId_CourtNumber", "PlayMatches", new[] { "SessionId", "CourtNumber" }, unique: true, filter: "\"Status\" = 'Active'");
        migrationBuilder.CreateIndex("IX_PlayMatchPlayers_MatchId_Position", "PlayMatchPlayers", new[] { "MatchId", "Position" }, unique: true);
        migrationBuilder.CreateIndex("IX_PlayMatchPlayers_PlayerId", "PlayMatchPlayers", "PlayerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PlayMatchPlayers");
        migrationBuilder.DropTable("PlayMatches");
    }
}
