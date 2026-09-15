using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class AddPlayRosterHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsRemoved", table: "PlaySessionPlayers",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "DisplayName", table: "PlayMatchPlayers",
            type: "character varying(80)", maxLength: 80, nullable: true);
        // Preserve the names currently shown by historical responses before enabling active renames.
        migrationBuilder.Sql("""
            UPDATE "PlayMatchPlayers" AS m SET "DisplayName" = p."DisplayName"
            FROM "PlaySessionPlayers" AS p WHERE p."Id" = m."PlayerId";
            """);
        migrationBuilder.AlterColumn<string>(name: "DisplayName", table: "PlayMatchPlayers",
            type: "character varying(80)", maxLength: 80, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(80)", oldMaxLength: 80, oldNullable: true);
        migrationBuilder.DropIndex(name: "IX_PlaySessionPlayers_SessionId_NormalizedDisplayName", table: "PlaySessionPlayers");
        migrationBuilder.CreateIndex(name: "IX_PlaySessionPlayers_SessionId_NormalizedDisplayName",
            table: "PlaySessionPlayers", columns: new[] { "SessionId", "NormalizedDisplayName" },
            unique: true, filter: "NOT \"IsRemoved\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not silently restore removed people or discard history when rolling back.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "PlaySessionPlayers" WHERE "IsRemoved") THEN
                    RAISE EXCEPTION 'Cannot roll back roster history while removed participants exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex(name: "IX_PlaySessionPlayers_SessionId_NormalizedDisplayName", table: "PlaySessionPlayers");
        migrationBuilder.CreateIndex(name: "IX_PlaySessionPlayers_SessionId_NormalizedDisplayName",
            table: "PlaySessionPlayers", columns: new[] { "SessionId", "NormalizedDisplayName" }, unique: true);
        migrationBuilder.DropColumn(name: "DisplayName", table: "PlayMatchPlayers");
        migrationBuilder.DropColumn(name: "IsRemoved", table: "PlaySessionPlayers");
    }
}
