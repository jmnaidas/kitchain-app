using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayReadyLineups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_Current",
                table: "PlayMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_Status",
                table: "PlayMatches");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "PlayMatches",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "IsLineupOverridden",
                table: "PlayMatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "LineupRevision",
                table: "PlayMatches",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_Current",
                table: "PlayMatches",
                sql: "\"Status\" NOT IN ('Active', 'Ready') OR \"IsCurrent\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_LineupRevision",
                table: "PlayMatches",
                sql: "\"LineupRevision\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_Status",
                table: "PlayMatches",
                sql: "(\"Status\" = 'Ready' AND \"StartedAt\" IS NULL AND \"CompletedAt\" IS NULL AND \"TeamAScore\" = 0 AND \"TeamBScore\" = 0) OR (\"Status\" = 'Active' AND \"StartedAt\" IS NOT NULL AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"StartedAt\" IS NOT NULL AND \"CompletedAt\" >= \"StartedAt\" AND \"CompletedAt\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_Current",
                table: "PlayMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_LineupRevision",
                table: "PlayMatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlayMatches_Status",
                table: "PlayMatches");

            migrationBuilder.DropColumn(
                name: "IsLineupOverridden",
                table: "PlayMatches");

            migrationBuilder.DropColumn(
                name: "LineupRevision",
                table: "PlayMatches");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "PlayMatches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_Current",
                table: "PlayMatches",
                sql: "\"Status\" <> 'Active' OR \"IsCurrent\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlayMatches_Status",
                table: "PlayMatches",
                sql: "(\"Status\" = 'Active' AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"CompletedAt\" >= \"StartedAt\" AND \"CompletedAt\" IS NOT NULL)");
        }
    }
}
