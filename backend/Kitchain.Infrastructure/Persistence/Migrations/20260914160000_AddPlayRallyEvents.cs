using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations;

public partial class AddPlayRallyEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Existing games retain their scores. No historical rallies can safely be invented.
        migrationBuilder.CreateTable(
            name: "PlayRallyEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false),
                Winner = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                PointAwarded = table.Column<bool>(type: "boolean", nullable: false),
                CallOut = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                TeamAScore = table.Column<int>(type: "integer", nullable: false),
                TeamBScore = table.Column<int>(type: "integer", nullable: false),
                ServingTeam = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                CurrentServerNumber = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayRallyEvents", x => x.Id);
                table.ForeignKey("FK_PlayRallyEvents_PlayMatches_MatchId", x => x.MatchId, "PlayMatches", "Id", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_PlayRallyEvents_Sequence", "\"Sequence\" > 0");
                table.CheckConstraint("CK_PlayRallyEvents_Winner", "\"Winner\" IN ('A', 'B')");
                table.CheckConstraint("CK_PlayRallyEvents_Scores", "\"TeamAScore\" >= 0 AND \"TeamBScore\" >= 0");
                table.CheckConstraint("CK_PlayRallyEvents_Service", "\"ServingTeam\" IN ('A', 'B') AND \"CurrentServerNumber\" IN (1, 2)");
                table.CheckConstraint("CK_PlayRallyEvents_CallOut", "\"CallOut\" IS NULL OR \"CallOut\" IN ('Drive', 'Dink', 'Lob', 'Fault', 'Out', 'Kitchen', 'ServiceBreak')");
            });
        migrationBuilder.CreateIndex(name: "IX_PlayRallyEvents_MatchId_Sequence", table: "PlayRallyEvents", columns: new[] { "MatchId", "Sequence" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("PlayRallyEvents");
}
