using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class AddPlayMatchScoring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "TeamAScore", table: "PlayMatches", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<int>(name: "TeamBScore", table: "PlayMatches", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>(name: "ServingTeam", table: "PlayMatches", type: "character varying(1)", maxLength: 1, nullable: false, defaultValue: "A");
        migrationBuilder.AddColumn<int>(name: "CurrentServerNumber", table: "PlayMatches", type: "integer", nullable: false, defaultValue: 2);
        migrationBuilder.AddCheckConstraint(name: "CK_PlayMatches_Scores", table: "PlayMatches", sql: "\"TeamAScore\" >= 0 AND \"TeamBScore\" >= 0");
        migrationBuilder.AddCheckConstraint(name: "CK_PlayMatches_Service", table: "PlayMatches", sql: "\"ServingTeam\" IN ('A', 'B') AND \"CurrentServerNumber\" IN (1, 2)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_PlayMatches_Scores", table: "PlayMatches");
        migrationBuilder.DropCheckConstraint(name: "CK_PlayMatches_Service", table: "PlayMatches");
        migrationBuilder.DropColumn(name: "TeamAScore", table: "PlayMatches");
        migrationBuilder.DropColumn(name: "TeamBScore", table: "PlayMatches");
        migrationBuilder.DropColumn(name: "ServingTeam", table: "PlayMatches");
        migrationBuilder.DropColumn(name: "CurrentServerNumber", table: "PlayMatches");
    }
}
