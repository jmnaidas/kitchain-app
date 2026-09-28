using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayNextRoundPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NextRoundJson",
                table: "PlaySessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NextRoundRevision",
                table: "PlaySessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextRoundJson",
                table: "PlaySessions");

            migrationBuilder.DropColumn(
                name: "NextRoundRevision",
                table: "PlaySessions");
        }
    }
}
