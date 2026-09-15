using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class RefinePlaySessionFlow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Mode", table: "PlaySessions", type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "QueueOnly");
        // Existing sessions were created with live scoring; retain that behavior.
        migrationBuilder.Sql("UPDATE \"PlaySessions\" SET \"Mode\" = 'LiveScoring'");
        migrationBuilder.AddColumn<bool>(name: "IsCurrent", table: "PlayMatches", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "Winner", table: "PlayMatches", type: "character varying(1)", maxLength: 1, nullable: true);
        // Previously completed matches have already rotated and must remain historical.
        migrationBuilder.Sql("UPDATE \"PlayMatches\" SET \"IsCurrent\" = TRUE WHERE \"Status\" = 'Active'");
        migrationBuilder.DropIndex(name: "IX_PlayMatches_SessionId_CourtNumber", table: "PlayMatches");
        migrationBuilder.CreateIndex(name: "IX_PlayMatches_SessionId_CourtNumber", table: "PlayMatches", columns: new[] { "SessionId", "CourtNumber" }, unique: true, filter: "\"IsCurrent\"");
        migrationBuilder.AddCheckConstraint(name: "CK_PlaySessions_Mode", table: "PlaySessions", sql: "\"Mode\" IN ('QueueOnly', 'LiveScoring')");
        migrationBuilder.AddCheckConstraint(name: "CK_PlayMatches_Current", table: "PlayMatches", sql: "\"Status\" <> 'Active' OR \"IsCurrent\"");
        migrationBuilder.AddCheckConstraint(name: "CK_PlayMatches_Winner", table: "PlayMatches", sql: "\"Winner\" IS NULL OR (\"Status\" = 'Completed' AND \"Winner\" IN ('A', 'B'))");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_PlaySessions_Mode", table: "PlaySessions");
        migrationBuilder.DropCheckConstraint(name: "CK_PlayMatches_Current", table: "PlayMatches");
        migrationBuilder.DropCheckConstraint(name: "CK_PlayMatches_Winner", table: "PlayMatches");
        migrationBuilder.DropIndex(name: "IX_PlayMatches_SessionId_CourtNumber", table: "PlayMatches");
        migrationBuilder.CreateIndex(name: "IX_PlayMatches_SessionId_CourtNumber", table: "PlayMatches", columns: new[] { "SessionId", "CourtNumber" }, unique: true, filter: "\"Status\" = 'Active'");
        migrationBuilder.DropColumn(name: "Mode", table: "PlaySessions");
        migrationBuilder.DropColumn(name: "IsCurrent", table: "PlayMatches");
        migrationBuilder.DropColumn(name: "Winner", table: "PlayMatches");
    }
}
