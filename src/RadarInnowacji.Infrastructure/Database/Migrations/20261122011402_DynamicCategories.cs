using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DynamicCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Change = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 600, nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    Signature = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectEmbeddings",
                columns: table => new
                {
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Vector = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    Model = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ClusterId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClusterSimilarity = table.Column<double>(type: "REAL", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectEmbeddings", x => x.InnovationProjectId);
                    table.ForeignKey(
                        name: "FK_ProjectEmbeddings_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CategoryProposalProjects",
                columns: table => new
                {
                    CategoryProposalId = table.Column<int>(type: "INTEGER", nullable: false),
                    InnovationProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryProposalProjects", x => new { x.CategoryProposalId, x.InnovationProjectId });
                    table.ForeignKey(
                        name: "FK_CategoryProposalProjects_CategoryProposals_CategoryProposalId",
                        column: x => x.CategoryProposalId,
                        principalTable: "CategoryProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryProposalProjects_InnovationProjects_InnovationProjectId",
                        column: x => x.InnovationProjectId,
                        principalTable: "InnovationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryHistories_CategoryId",
                table: "CategoryHistories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryHistories_ChangedAt",
                table: "CategoryHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryProposalProjects_InnovationProjectId",
                table: "CategoryProposalProjects",
                column: "InnovationProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryProposals_CreatedAt",
                table: "CategoryProposals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryProposals_Signature",
                table: "CategoryProposals",
                column: "Signature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryProposals_Status",
                table: "CategoryProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEmbeddings_ClusterId",
                table: "ProjectEmbeddings",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectEmbeddings_Model",
                table: "ProjectEmbeddings",
                column: "Model");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryHistories");

            migrationBuilder.DropTable(
                name: "CategoryProposalProjects");

            migrationBuilder.DropTable(
                name: "ProjectEmbeddings");

            migrationBuilder.DropTable(
                name: "CategoryProposals");
        }
    }
}
