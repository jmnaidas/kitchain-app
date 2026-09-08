using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    public partial class AddCourtPhotos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourtPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourtId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtPhotos", x => x.Id);
                    table.CheckConstraint("CK_CourtPhotos_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_CourtPhotos_ImageUrl", "\"ImageUrl\" ~ '^https?://[^[:space:]/?#@]+([/?#][^[:space:]]*)?$'");
                    table.ForeignKey(name: "FK_CourtPhotos_Courts_CourtId", column: x => x.CourtId,
                        principalTable: "Courts", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_CourtPhotos_OnePrimaryPerCourt", table: "CourtPhotos", column: "CourtId",
                unique: true, filter: "\"IsPrimary\"");
            migrationBuilder.CreateIndex(
                name: "IX_CourtPhotos_CourtId_IsPrimary_DisplayOrder_Id", table: "CourtPhotos",
                columns: new[] { "CourtId", "IsPrimary", "DisplayOrder", "Id" },
                descending: new[] { false, true, false, false });
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "CourtPhotos");
    }
}
