using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Etap7ProjectLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MergedIntoProjectId",
                table: "InnovationProjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectMergeHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMergeHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMergeProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false),
                    Signals = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Signature = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecisionNote = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMergeProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMergeProposals_InnovationProjects_SourceProjectId",
                        column: x => x.SourceProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMergeProposals_InnovationProjects_TargetProjectId",
                        column: x => x.TargetProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InnovationProjects_MergedIntoProjectId",
                table: "InnovationProjects",
                column: "MergedIntoProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeHistories_ChangedAt",
                table: "ProjectMergeHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeHistories_TargetProjectId",
                table: "ProjectMergeHistories",
                column: "TargetProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeProposals_CreatedAt",
                table: "ProjectMergeProposals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeProposals_Signature",
                table: "ProjectMergeProposals",
                column: "Signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeProposals_SourceProjectId",
                table: "ProjectMergeProposals",
                column: "SourceProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeProposals_Status",
                table: "ProjectMergeProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMergeProposals_TargetProjectId",
                table: "ProjectMergeProposals",
                column: "TargetProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_InnovationProjects_InnovationProjects_MergedIntoProjectId",
                table: "InnovationProjects",
                column: "MergedIntoProjectId",
                principalTable: "InnovationProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InnovationProjects_InnovationProjects_MergedIntoProjectId",
                table: "InnovationProjects");

            migrationBuilder.DropTable(
                name: "ProjectMergeHistories");

            migrationBuilder.DropTable(
                name: "ProjectMergeProposals");

            migrationBuilder.DropIndex(
                name: "IX_InnovationProjects_MergedIntoProjectId",
                table: "InnovationProjects");

            migrationBuilder.DropColumn(
                name: "MergedIntoProjectId",
                table: "InnovationProjects");
        }
    }
}
