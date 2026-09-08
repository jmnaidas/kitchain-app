using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourtSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    NumberOfCourts = table.Column<int>(type: "integer", nullable: false),
                    IndoorOutdoor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Surface = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpeningHours = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartingPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PriceUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SocialUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BookingUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    BookingMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtSubmissions", x => x.Id);
                    table.CheckConstraint("CK_CourtSubmissions_BookingMethod", "\"BookingMethod\" IN ('ExternalPlatform', 'Website', 'GoogleForm', 'Phone', 'Message', 'WalkIn', 'Other')");
                    table.CheckConstraint("CK_CourtSubmissions_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_CourtSubmissions_Currency", "\"CurrencyCode\" IS NULL OR \"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_CourtSubmissions_IndoorOutdoor", "\"IndoorOutdoor\" IN ('Indoor', 'Outdoor', 'Mixed')");
                    table.CheckConstraint("CK_CourtSubmissions_NumberOfCourts", "\"NumberOfCourts\" > 0");
                    table.CheckConstraint("CK_CourtSubmissions_Price", "\"StartingPrice\" IS NULL OR (\"StartingPrice\" >= 0 AND \"CurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_CourtSubmissions_PriceUnit", "\"PriceUnit\" IS NULL OR (\"StartingPrice\" IS NOT NULL AND \"PriceUnit\" IN ('PerHour', 'PerPerson', 'PerSession'))");
                    table.CheckConstraint("CK_CourtSubmissions_RequiredText", "length(trim(\"Name\")) > 0 AND length(trim(\"City\")) > 0 AND length(trim(\"Address\")) > 0");
                    table.CheckConstraint("CK_CourtSubmissions_Status", "\"Status\" IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_CourtSubmissions_Timestamps", "\"UpdatedAt\" >= \"SubmittedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "CourtSubmissionAmenities",
                columns: table => new
                {
                    CourtSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmenityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtSubmissionAmenities", x => new { x.CourtSubmissionId, x.AmenityCode });
                    table.ForeignKey(
                        name: "FK_CourtSubmissionAmenities_Amenities_AmenityCode",
                        column: x => x.AmenityCode,
                        principalTable: "Amenities",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourtSubmissionAmenities_CourtSubmissions_CourtSubmissionId",
                        column: x => x.CourtSubmissionId,
                        principalTable: "CourtSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourtSubmissionAmenities_AmenityCode",
                table: "CourtSubmissionAmenities",
                column: "AmenityCode");

            migrationBuilder.CreateIndex(
                name: "IX_CourtSubmissions_Status_Name_Id",
                table: "CourtSubmissions",
                columns: new[] { "Status", "Name", "Id" });
            migrationBuilder.CreateTable(
                name: "CourtSubmissionPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourtSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtSubmissionPhotos", x => x.Id);
                    table.CheckConstraint("CK_CourtSubmissionPhotos_DisplayOrder", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_CourtSubmissionPhotos_ImageUrl", "\"ImageUrl\" ~ '^https?://[^[:space:]/?#@]+([/?#][^[:space:]]*)?$'");
                    table.ForeignKey(name: "FK_CourtSubmissionPhotos_CourtSubmissions_CourtSubmissionId", column: x => x.CourtSubmissionId,
                        principalTable: "CourtSubmissions", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_CourtSubmissionPhotos_OnePrimaryPerCourt", table: "CourtSubmissionPhotos", column: "CourtSubmissionId",
                unique: true, filter: "\"IsPrimary\"");
            migrationBuilder.CreateIndex(
                name: "IX_CourtSubmissionPhotos_Ordered", table: "CourtSubmissionPhotos",
                columns: new[] { "CourtSubmissionId", "IsPrimary", "DisplayOrder", "Id" },
                descending: new[] { false, true, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CourtSubmissionPhotos");
            migrationBuilder.DropTable(
                name: "CourtSubmissionAmenities");

            migrationBuilder.DropTable(
                name: "CourtSubmissions");
        }
    }
}
