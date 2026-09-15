using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class AddPlayFairRotationAndDateRange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(name: "EndDate", table: "PlaySessions", type: "date", nullable: true);
        migrationBuilder.Sql("UPDATE \"PlaySessions\" SET \"EndDate\" = \"SessionDate\"");
        migrationBuilder.AlterColumn<DateOnly>(name: "EndDate", table: "PlaySessions", type: "date", nullable: false,
            oldClrType: typeof(DateOnly), oldType: "date", oldNullable: true);
        migrationBuilder.DropCheckConstraint(name: "CK_PlaySessions_Schedule", table: "PlaySessions");
        migrationBuilder.AddCheckConstraint(name: "CK_PlaySessions_Schedule", table: "PlaySessions",
            sql: "(\"EndDate\" + \"EndTime\") > (\"SessionDate\" + \"StartTime\")");
        migrationBuilder.AddColumn<long>(name: "AdjustedGamesStarted", table: "PlaySessionPlayers", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<long>(name: "MissedOpportunities", table: "PlaySessionPlayers", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "WaitingSince", table: "PlaySessionPlayers", type: "timestamp with time zone", nullable: true);
        // Historical rest/wait events were not recorded. Start wait credit at the migration boundary.
        migrationBuilder.Sql("""
            UPDATE "PlaySessionPlayers" p SET
              "AdjustedGamesStarted" = (SELECT COUNT(*) FROM "PlayMatchPlayers" mp WHERE mp."PlayerId" = p."Id"),
              "WaitingSince" = CASE WHEN p."State" = 'Waiting' THEN CURRENT_TIMESTAMP ELSE NULL END;
            """);
        migrationBuilder.AddCheckConstraint(name: "CK_PlaySessionPlayers_Fairness", table: "PlaySessionPlayers",
            sql: "\"AdjustedGamesStarted\" >= 0 AND \"MissedOpportunities\" >= 0");
        migrationBuilder.AddCheckConstraint(name: "CK_PlaySessionPlayers_WaitingSince", table: "PlaySessionPlayers",
            sql: "(\"State\" = 'Waiting') = (\"WaitingSince\" IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverting cannot silently discard an overnight session's end date.
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM "PlaySessions" WHERE "EndDate" <> "SessionDate") THEN
                RAISE EXCEPTION 'Cannot revert date ranges while multi-day Play sessions exist.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint(name: "CK_PlaySessionPlayers_Fairness", table: "PlaySessionPlayers");
        migrationBuilder.DropCheckConstraint(name: "CK_PlaySessionPlayers_WaitingSince", table: "PlaySessionPlayers");
        migrationBuilder.DropColumn(name: "AdjustedGamesStarted", table: "PlaySessionPlayers");
        migrationBuilder.DropColumn(name: "MissedOpportunities", table: "PlaySessionPlayers");
        migrationBuilder.DropColumn(name: "WaitingSince", table: "PlaySessionPlayers");
        migrationBuilder.DropCheckConstraint(name: "CK_PlaySessions_Schedule", table: "PlaySessions");
        migrationBuilder.DropColumn(name: "EndDate", table: "PlaySessions");
        migrationBuilder.AddCheckConstraint(name: "CK_PlaySessions_Schedule", table: "PlaySessions", sql: "\"EndTime\" > \"StartTime\"");
    }
}