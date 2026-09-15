using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayRotationPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions",
                sql: "\"RotationMode\" IN ('FairRotation', 'WinnersStay', 'ChallengersStay', 'SplitTeams') AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlaySessions_DefaultModes",
                table: "PlaySessions",
                sql: "\"RotationMode\" = 'FairRotation' AND \"ScoringMode\" = 'Traditional' AND \"GameTo\" = 11 AND \"WinBy\" = 2");
        }
    }
}
