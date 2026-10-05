using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayGameTimer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimerDurationMinutes",
                table: "PlayMatches",
                type: "integer",
                nullable: true);

            // Do not impose a timer retroactively on games already started.
            migrationBuilder.Sql("UPDATE \"PlayMatches\" SET \"TimerDurationMinutes\" = 15 WHERE \"Status\" = 'Ready'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_TimerDuration",
                table: "PlayMatches",
                sql: "\"TimerDurationMinutes\" IS NULL OR \"TimerDurationMinutes\" IN (10, 15, 20)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_TimerDuration",
                table: "PlayMatches");

            migrationBuilder.DropColumn(
                name: "TimerDurationMinutes",
                table: "PlayMatches");
        }
    }
}
