using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayPlayerSkillLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions");

            migrationBuilder.AddColumn<string>(
                name: "SkillLevel",
                table: "PlaySessionPlayers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions",
                sql: "\"RotationMode\" IN ('FairRotation', 'WinnersStay', 'ChallengersStay', 'SplitTeams', 'BalancedRotation') AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaySessionPlayers_SkillLevel",
                table: "PlaySessionPlayers",
                sql: "\"SkillLevel\" IS NULL OR \"SkillLevel\" IN ('Beginner', 'Novice', 'LowIntermediate', 'HighIntermediate', 'Advanced')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaySessionPlayers_SkillLevel",
                table: "PlaySessionPlayers");

            migrationBuilder.DropColumn(
                name: "SkillLevel",
                table: "PlaySessionPlayers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions",
                sql: "\"RotationMode\" IN ('FairRotation', 'WinnersStay', 'ChallengersStay', 'SplitTeams') AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");
        }
    }
}
