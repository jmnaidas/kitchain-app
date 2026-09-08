using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourtsDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Amenities",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenities", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Courts",
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
                    DataSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courts", x => x.Id);
                    table.CheckConstraint("CK_Courts_BookingMethod", "\"BookingMethod\" IN ('ExternalPlatform', 'Website', 'GoogleForm', 'Phone', 'Message', 'WalkIn', 'Other')");
                    table.CheckConstraint("CK_Courts_Coordinates", "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" BETWEEN -90 AND 90 AND \"Longitude\" BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_Courts_Currency", "\"CurrencyCode\" IS NULL OR \"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_Courts_DataSource", "\"DataSource\" IN ('OwnerSupplied', 'CommunitySupplied', 'KitchainCurated')");
                    table.CheckConstraint("CK_Courts_IndoorOutdoor", "\"IndoorOutdoor\" IN ('Indoor', 'Outdoor', 'Mixed')");
                    table.CheckConstraint("CK_Courts_NumberOfCourts", "\"NumberOfCourts\" > 0");
                    table.CheckConstraint("CK_Courts_Price", "\"StartingPrice\" IS NULL OR (\"StartingPrice\" >= 0 AND \"CurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_Courts_PriceUnit", "\"PriceUnit\" IS NULL OR (\"StartingPrice\" IS NOT NULL AND \"PriceUnit\" IN ('PerHour', 'PerPerson', 'PerSession'))");
                    table.CheckConstraint("CK_Courts_RequiredText", "length(trim(\"Name\")) > 0 AND length(trim(\"City\")) > 0 AND length(trim(\"Address\")) > 0");
                    table.CheckConstraint("CK_Courts_Status", "\"Status\" IN ('Draft', 'Published', 'Inactive')");
                    table.CheckConstraint("CK_Courts_Timestamps", "\"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "CourtAmenities",
                columns: table => new
                {
                    CourtId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmenityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourtAmenities", x => new { x.CourtId, x.AmenityCode });
                    table.ForeignKey(
                        name: "FK_CourtAmenities_Amenities_AmenityCode",
                        column: x => x.AmenityCode,
                        principalTable: "Amenities",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourtAmenities_Courts_CourtId",
                        column: x => x.CourtId,
                        principalTable: "Courts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Amenities",
                column: "Code",
                values: new object[]
                {
                    "AirConditioning",
                    "BallOrEquipmentRental",
                    "ChangingRoom",
                    "Coaching",
                    "FoodAndDrinks",
                    "Lockers",
                    "Other",
                    "PaddleRental",
                    "Parking",
                    "ProShop",
                    "Restroom",
                    "SeatingOrWaitingArea",
                    "Shower"
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourtAmenities_AmenityCode",
                table: "CourtAmenities",
                column: "AmenityCode");

            migrationBuilder.CreateIndex(
                name: "IX_Courts_Status_Name_Id",
                table: "Courts",
                columns: new[] { "Status", "Name", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourtAmenities");

            migrationBuilder.DropTable(
                name: "Amenities");

            migrationBuilder.DropTable(
                name: "Courts");
        }
    }
}
