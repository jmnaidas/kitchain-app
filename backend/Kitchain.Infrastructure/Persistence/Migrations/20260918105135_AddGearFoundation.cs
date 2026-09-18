using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGearFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GearBrands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearBrands", x => x.Id);
                    table.CheckConstraint("CK_GearBrands_Identity", "length(trim(\"Name\")) > 0 AND \"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_GearBrands_Time", "\"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "GearDataSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PublisherDomain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearDataSources", x => x.Id);
                    table.CheckConstraint("CK_GearDataSources_Required", "length(trim(\"Name\")) > 0 AND (\"Type\" = 'Manual' OR \"Url\" IS NOT NULL)");
                    table.CheckConstraint("CK_GearDataSources_Time", "\"LastCheckedAt\" >= \"RetrievedAt\" AND \"CreatedAt\" >= \"RetrievedAt\"");
                    table.CheckConstraint("CK_GearDataSources_Type", "\"Type\" IN ('Manufacturer','Retailer','Marketplace','IndependentTest','Editorial','Manual')");
                });

            migrationBuilder.CreateTable(
                name: "GearPaddles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelFamily = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearPaddles", x => x.Id);
                    table.CheckConstraint("CK_GearPaddles_Identity", "length(trim(\"Name\")) > 0 AND \"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_GearPaddles_Status", "\"Status\" IN ('Draft','Published','Archived')");
                    table.CheckConstraint("CK_GearPaddles_Time", "\"UpdatedAt\" >= \"CreatedAt\"");
                    table.CheckConstraint("CK_GearPaddles_Year", "\"ReleaseYear\" IS NULL OR \"ReleaseYear\" BETWEEN 1965 AND 2200");
                    table.ForeignKey(
                        name: "FK_GearPaddles_GearBrands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "GearBrands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearPaddleVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaddleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ManufacturerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NormalizedSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearPaddleVariants", x => x.Id);
                    table.CheckConstraint("CK_GearPaddleVariants_Identity", "length(trim(\"Name\")) > 0 AND \"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_GearPaddleVariants_Status", "\"Status\" IN ('Draft','Published','Archived')");
                    table.CheckConstraint("CK_GearPaddleVariants_Time", "\"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_GearPaddleVariants_GearPaddles_PaddleId",
                        column: x => x.PaddleId,
                        principalTable: "GearPaddles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearImportCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BrandSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PaddleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaddleSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    VariantName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VariantSlug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ManufacturerSku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EvidenceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThicknessMm = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    AdvertisedWeightMinOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    AdvertisedWeightMaxOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    LengthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    WidthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    HandleLengthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    GripCircumferenceInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    FaceMaterial = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CoreMaterial = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Construction = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SwingWeightKgCm2 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    TwistWeightKgCm2 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    MeasuredWeightOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MatchedVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearImportCandidates", x => x.Id);
                    table.CheckConstraint("CK_GearImportCandidates_Decision", "(\"Status\" = 'Pending' AND \"ReviewedAt\" IS NULL AND \"MatchedVariantId\" IS NULL) OR (\"Status\" = 'Rejected' AND \"ReviewedAt\" IS NOT NULL AND \"MatchedVariantId\" IS NULL) OR (\"Status\" = 'Approved' AND \"ReviewedAt\" IS NOT NULL AND \"MatchedVariantId\" IS NOT NULL)");
                    table.CheckConstraint("CK_GearImportCandidates_Evidence", "\"EvidenceType\" IN ('ManufacturerStated','RetailerStated','IndependentlyMeasured','KitchainVerified')");
                    table.CheckConstraint("CK_GearImportCandidates_Positive", "(\"ThicknessMm\" IS NULL OR \"ThicknessMm\" > 0) AND (\"AdvertisedWeightMinOz\" IS NULL OR \"AdvertisedWeightMinOz\" > 0) AND (\"AdvertisedWeightMaxOz\" IS NULL OR \"AdvertisedWeightMaxOz\" > 0) AND (\"LengthInches\" IS NULL OR \"LengthInches\" > 0) AND (\"WidthInches\" IS NULL OR \"WidthInches\" > 0) AND (\"HandleLengthInches\" IS NULL OR \"HandleLengthInches\" > 0) AND (\"GripCircumferenceInches\" IS NULL OR \"GripCircumferenceInches\" > 0) AND (\"SwingWeightKgCm2\" IS NULL OR \"SwingWeightKgCm2\" > 0) AND (\"TwistWeightKgCm2\" IS NULL OR \"TwistWeightKgCm2\" > 0) AND (\"MeasuredWeightOz\" IS NULL OR \"MeasuredWeightOz\" > 0)");
                    table.CheckConstraint("CK_GearImportCandidates_Ranges", "(\"AdvertisedWeightMinOz\" IS NULL OR \"AdvertisedWeightMaxOz\" IS NULL OR \"AdvertisedWeightMinOz\" <= \"AdvertisedWeightMaxOz\") AND (\"HandleLengthInches\" IS NULL OR \"LengthInches\" IS NULL OR \"HandleLengthInches\" <= \"LengthInches\")");
                    table.CheckConstraint("CK_GearImportCandidates_Shape", "\"Shape\" IS NULL OR \"Shape\" IN ('Standard','Widebody','Hybrid','Elongated','Other')");
                    table.CheckConstraint("CK_GearImportCandidates_Time", "\"CreatedAt\" >= \"ObservedAt\" AND (\"ReviewedAt\" IS NULL OR \"ReviewedAt\" >= \"CreatedAt\")");
                    table.ForeignKey(
                        name: "FK_GearImportCandidates_GearDataSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "GearDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GearImportCandidates_GearPaddleVariants_MatchedVariantId",
                        column: x => x.MatchedVariantId,
                        principalTable: "GearPaddleVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearPaddleImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaddleVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearPaddleImages", x => x.Id);
                    table.CheckConstraint("CK_GearPaddleImages_Order", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_GearPaddleImages_GearDataSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "GearDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GearPaddleImages_GearPaddleVariants_PaddleVariantId",
                        column: x => x.PaddleVariantId,
                        principalTable: "GearPaddleVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearPaddleListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaddleVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    OriginalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    InStock = table.Column<bool>(type: "boolean", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearPaddleListings", x => x.Id);
                    table.CheckConstraint("CK_GearPaddleListings_Currency", "\"CurrencyCode\" ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("CK_GearPaddleListings_Price", "(\"Price\" IS NULL OR \"Price\" >= 0) AND (\"OriginalPrice\" IS NULL OR \"OriginalPrice\" >= 0)");
                    table.CheckConstraint("CK_GearPaddleListings_Time", "\"UpdatedAt\" >= \"CreatedAt\" AND \"CreatedAt\" >= \"LastCheckedAt\"");
                    table.ForeignKey(
                        name: "FK_GearPaddleListings_GearDataSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "GearDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GearPaddleListings_GearPaddleVariants_PaddleVariantId",
                        column: x => x.PaddleVariantId,
                        principalTable: "GearPaddleVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearPerformanceProfiles",
                columns: table => new
                {
                    PaddleVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Power = table.Column<int>(type: "integer", nullable: true),
                    Control = table.Column<int>(type: "integer", nullable: true),
                    Forgiveness = table.Column<int>(type: "integer", nullable: true),
                    HandSpeed = table.Column<int>(type: "integer", nullable: true),
                    Spin = table.Column<int>(type: "integer", nullable: true),
                    SweetSpot = table.Column<int>(type: "integer", nullable: true),
                    OverallStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearPerformanceProfiles", x => x.PaddleVariantId);
                    table.CheckConstraint("CK_GearPerformanceProfiles_Method", "length(trim(\"MethodVersion\")) > 0");
                    table.CheckConstraint("CK_GearPerformanceProfiles_Ratings", "(\"Power\" IS NULL OR \"Power\" BETWEEN 1 AND 10) AND (\"Control\" IS NULL OR \"Control\" BETWEEN 1 AND 10) AND (\"Forgiveness\" IS NULL OR \"Forgiveness\" BETWEEN 1 AND 10) AND (\"HandSpeed\" IS NULL OR \"HandSpeed\" BETWEEN 1 AND 10) AND (\"Spin\" IS NULL OR \"Spin\" BETWEEN 1 AND 10) AND (\"SweetSpot\" IS NULL OR \"SweetSpot\" BETWEEN 1 AND 10)");
                    table.CheckConstraint("CK_GearPerformanceProfiles_Status", "\"Status\" IN ('Draft','Published','Archived')");
                    table.CheckConstraint("CK_GearPerformanceProfiles_Style", "\"OverallStyle\" IS NULL OR \"OverallStyle\" IN ('Power','Control','Balanced')");
                    table.ForeignKey(
                        name: "FK_GearPerformanceProfiles_GearPaddleVariants_PaddleVariantId",
                        column: x => x.PaddleVariantId,
                        principalTable: "GearPaddleVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GearSpecificationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaddleVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThicknessMm = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    AdvertisedWeightMinOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    AdvertisedWeightMaxOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    LengthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    WidthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    HandleLengthInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    GripCircumferenceInches = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    FaceMaterial = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CoreMaterial = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Construction = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SwingWeightKgCm2 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    TwistWeightKgCm2 = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    MeasuredWeightOz = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GearSpecificationEvidence", x => x.Id);
                    table.CheckConstraint("CK_GearSpecificationEvidence_Positive", "(\"ThicknessMm\" IS NULL OR \"ThicknessMm\" > 0) AND (\"AdvertisedWeightMinOz\" IS NULL OR \"AdvertisedWeightMinOz\" > 0) AND (\"AdvertisedWeightMaxOz\" IS NULL OR \"AdvertisedWeightMaxOz\" > 0) AND (\"LengthInches\" IS NULL OR \"LengthInches\" > 0) AND (\"WidthInches\" IS NULL OR \"WidthInches\" > 0) AND (\"HandleLengthInches\" IS NULL OR \"HandleLengthInches\" > 0) AND (\"GripCircumferenceInches\" IS NULL OR \"GripCircumferenceInches\" > 0) AND (\"SwingWeightKgCm2\" IS NULL OR \"SwingWeightKgCm2\" > 0) AND (\"TwistWeightKgCm2\" IS NULL OR \"TwistWeightKgCm2\" > 0) AND (\"MeasuredWeightOz\" IS NULL OR \"MeasuredWeightOz\" > 0)");
                    table.CheckConstraint("CK_GearSpecificationEvidence_Ranges", "(\"AdvertisedWeightMinOz\" IS NULL OR \"AdvertisedWeightMaxOz\" IS NULL OR \"AdvertisedWeightMinOz\" <= \"AdvertisedWeightMaxOz\") AND (\"HandleLengthInches\" IS NULL OR \"LengthInches\" IS NULL OR \"HandleLengthInches\" <= \"LengthInches\")");
                    table.CheckConstraint("CK_GearSpecificationEvidence_Shape", "\"Shape\" IS NULL OR \"Shape\" IN ('Standard','Widebody','Hybrid','Elongated','Other')");
                    table.CheckConstraint("CK_GearSpecificationEvidence_Time", "\"CreatedAt\" >= \"ObservedAt\"");
                    table.CheckConstraint("CK_GearSpecificationEvidence_Type", "\"Type\" IN ('ManufacturerStated','RetailerStated','IndependentlyMeasured','KitchainVerified')");
                    table.ForeignKey(
                        name: "FK_GearSpecificationEvidence_GearDataSources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "GearDataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GearSpecificationEvidence_GearPaddleVariants_PaddleVariantId",
                        column: x => x.PaddleVariantId,
                        principalTable: "GearPaddleVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GearBrands_NormalizedName",
                table: "GearBrands",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearBrands_Slug",
                table: "GearBrands",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearDataSources_Name_Id",
                table: "GearDataSources",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearImportCandidates_MatchedVariantId",
                table: "GearImportCandidates",
                column: "MatchedVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_GearImportCandidates_SourceId",
                table: "GearImportCandidates",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GearImportCandidates_Status_CreatedAt_Id",
                table: "GearImportCandidates",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleImages_PaddleVariantId",
                table: "GearPaddleImages",
                column: "PaddleVariantId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleImages_PaddleVariantId_SortOrder_Id",
                table: "GearPaddleImages",
                columns: new[] { "PaddleVariantId", "SortOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleImages_SourceId",
                table: "GearPaddleImages",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleListings_PaddleVariantId_LastCheckedAt_Id",
                table: "GearPaddleListings",
                columns: new[] { "PaddleVariantId", "LastCheckedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleListings_SourceId",
                table: "GearPaddleListings",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddles_BrandId_Slug",
                table: "GearPaddles",
                columns: new[] { "BrandId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddles_Status_Name_Id",
                table: "GearPaddles",
                columns: new[] { "Status", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleVariants_PaddleId_NormalizedSku",
                table: "GearPaddleVariants",
                columns: new[] { "PaddleId", "NormalizedSku" },
                unique: true,
                filter: "\"NormalizedSku\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleVariants_PaddleId_Slug",
                table: "GearPaddleVariants",
                columns: new[] { "PaddleId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GearPaddleVariants_PaddleId_Status",
                table: "GearPaddleVariants",
                columns: new[] { "PaddleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GearSpecificationEvidence_PaddleVariantId_ObservedAt_Id",
                table: "GearSpecificationEvidence",
                columns: new[] { "PaddleVariantId", "ObservedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GearSpecificationEvidence_SourceId",
                table: "GearSpecificationEvidence",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GearImportCandidates");

            migrationBuilder.DropTable(
                name: "GearPaddleImages");

            migrationBuilder.DropTable(
                name: "GearPaddleListings");

            migrationBuilder.DropTable(
                name: "GearPerformanceProfiles");

            migrationBuilder.DropTable(
                name: "GearSpecificationEvidence");

            migrationBuilder.DropTable(
                name: "GearDataSources");

            migrationBuilder.DropTable(
                name: "GearPaddleVariants");

            migrationBuilder.DropTable(
                name: "GearPaddles");

            migrationBuilder.DropTable(
                name: "GearBrands");
        }
    }
}
