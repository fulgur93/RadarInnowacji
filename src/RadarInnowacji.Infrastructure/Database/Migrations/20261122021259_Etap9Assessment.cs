using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Etap9Assessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContactConsent",
                table: "InnovationProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "InnovationProjects",
                type: "TEXT",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Creators",
                table: "InnovationProjects",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InnovationKind",
                table: "InnovationProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Institution",
                table: "InnovationProjects",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstitutionType",
                table: "InnovationProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PipelineStage",
                table: "InnovationProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileUpdatedAt",
                table: "InnovationProjects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "InnovationProjects",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CriterionOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Criterion = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriterionOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriterionOverrides_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalScore = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAssessments_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectPipelineEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStage = table.Column<int>(type: "INTEGER", nullable: false),
                    ToStage = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectPipelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectPipelineEvents_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Sectors = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Paths = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MinimumStage = table.Column<int>(type: "INTEGER", nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsExample = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentCriterionScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectAssessmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Criterion = table.Column<int>(type: "INTEGER", nullable: false),
                    AutomaticScore = table.Column<int>(type: "INTEGER", nullable: false),
                    FinalScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ExpertComment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentCriterionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentCriterionScores_ProjectAssessments_ProjectAssessmentId",
                        column: x => x.ProjectAssessmentId,
                        principalTable: "ProjectAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectAssessmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRecommendations_ProjectAssessments_ProjectAssessmentId",
                        column: x => x.ProjectAssessmentId,
                        principalTable: "ProjectAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientId = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReferrals_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectReferrals_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InnovationProjects_PipelineStage",
                table: "InnovationProjects",
                column: "PipelineStage");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentCriterionScores_ProjectAssessmentId_Criterion",
                table: "AssessmentCriterionScores",
                columns: new[] { "ProjectAssessmentId", "Criterion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CriterionOverrides_InnovationProjectId_Criterion_IsActive",
                table: "CriterionOverrides",
                columns: new[] { "InnovationProjectId", "Criterion", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssessments_InnovationProjectId_IsCurrent",
                table: "ProjectAssessments",
                columns: new[] { "InnovationProjectId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAssessments_TotalScore",
                table: "ProjectAssessments",
                column: "TotalScore");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectPipelineEvents_InnovationProjectId_CreatedAt",
                table: "ProjectPipelineEvents",
                columns: new[] { "InnovationProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRecommendations_Path",
                table: "ProjectRecommendations",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRecommendations_ProjectAssessmentId",
                table: "ProjectRecommendations",
                column: "ProjectAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReferrals_InnovationProjectId",
                table: "ProjectReferrals",
                column: "InnovationProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReferrals_RecipientId",
                table: "ProjectReferrals",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_IsActive",
                table: "Recipients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_Name",
                table: "Recipients",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentCriterionScores");

            migrationBuilder.DropTable(
                name: "CriterionOverrides");

            migrationBuilder.DropTable(
                name: "ProjectPipelineEvents");

            migrationBuilder.DropTable(
                name: "ProjectRecommendations");

            migrationBuilder.DropTable(
                name: "ProjectReferrals");

            migrationBuilder.DropTable(
                name: "ProjectAssessments");

            migrationBuilder.DropTable(
                name: "Recipients");

            migrationBuilder.DropIndex(
                name: "IX_InnovationProjects_PipelineStage",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "ContactConsent",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "Creators",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "InnovationKind",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "Institution",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "InstitutionType",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "PipelineStage",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "ProfileUpdatedAt",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "InnovationProjects");
        }
    }
}
