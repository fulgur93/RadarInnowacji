using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ParentCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InnovationProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Problem = table.Column<string>(type: "TEXT", nullable: true),
                    ProposedSolution = table.Column<string>(type: "TEXT", nullable: true),
                    ProjectStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EarliestKnownDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FreshnessScore = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InnovationProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Radar = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScanIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastScanDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSuccessfulScan = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCategories",
                columns: table => new
                {
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCategories", x => new { x.InnovationProjectId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProjectCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectCategories_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Discoveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RawContent = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    ContentLength = table.Column<int>(type: "INTEGER", nullable: false),
                    IsContentTruncated = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EarliestKnownDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Author = table.Column<string>(type: "TEXT", nullable: true),
                    Organization = table.Column<string>(type: "TEXT", nullable: true),
                    Languages = table.Column<string>(type: "TEXT", nullable: false),
                    Technologies = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryIds = table.Column<string>(type: "TEXT", nullable: false),
                    AIProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPotentialInnovation = table.Column<bool>(type: "INTEGER", nullable: false),
                    AIConfidence = table.Column<int>(type: "INTEGER", nullable: true),
                    AIAnalysis = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ProcessingStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discoveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discoveries_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsNew = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanHistory_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscoveryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", nullable: false),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IsDemo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiAnalyses_Discoveries_DiscoveryId",
                        column: x => x.DiscoveryId,
                        principalTable: "Discoveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveryProjects",
                columns: table => new
                {
                    DiscoveryId = table.Column<int>(type: "INTEGER", nullable: false),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryProjects", x => new { x.DiscoveryId, x.InnovationProjectId });
                    table.ForeignKey(
                        name: "FK_DiscoveryProjects_Discoveries_DiscoveryId",
                        column: x => x.DiscoveryId,
                        principalTable: "Discoveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscoveryProjects_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAnalyses_DiscoveryId_AnalyzedAt",
                table: "AiAnalyses",
                columns: new[] { "DiscoveryId", "AnalyzedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_ContentHash",
                table: "Discoveries",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_DiscoveredAt",
                table: "Discoveries",
                column: "DiscoveredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_IsPotentialInnovation",
                table: "Discoveries",
                column: "IsPotentialInnovation");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_LastActivityAt",
                table: "Discoveries",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_ProcessingStatus",
                table: "Discoveries",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_PublishedAt",
                table: "Discoveries",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_SourceId_ExternalId",
                table: "Discoveries",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discoveries_Status",
                table: "Discoveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryProjects_InnovationProjectId",
                table: "DiscoveryProjects",
                column: "InnovationProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InnovationProjects_FirstDiscoveredAt",
                table: "InnovationProjects",
                column: "FirstDiscoveredAt");

            migrationBuilder.CreateIndex(
                name: "IX_InnovationProjects_FreshnessScore",
                table: "InnovationProjects",
                column: "FreshnessScore");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCategories_CategoryId",
                table: "ProjectCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanHistory_SourceId_StartedAt",
                table: "ScanHistory",
                columns: new[] { "SourceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Name",
                table: "Sources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Radar_IsActive",
                table: "Sources",
                columns: new[] { "Radar", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAnalyses");

            migrationBuilder.DropTable(
                name: "DiscoveryProjects");

            migrationBuilder.DropTable(
                name: "ProjectCategories");

            migrationBuilder.DropTable(
                name: "ScanHistory");

            migrationBuilder.DropTable(
                name: "Discoveries");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "InnovationProjects");

            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
