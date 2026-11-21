using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RepositoryInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscoveryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Forks = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenIssues = table.Column<int>(type: "INTEGER", nullable: true),
                    License = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    HomepageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    DemoUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    DefaultBranch = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReadmePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsFork = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Languages = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false, defaultValue: "{}"),
                    HasReadme = table.Column<bool>(type: "INTEGER", nullable: true),
                    ReadmeLength = table.Column<int>(type: "INTEGER", nullable: true),
                    MetadataUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetailsFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DetailsError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Discoveries_DiscoveryId",
                        column: x => x.DiscoveryId,
                        principalTable: "Discoveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_DetailsFetchedAt",
                table: "Repositories",
                column: "DetailsFetchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_DiscoveryId",
                table: "Repositories",
                column: "DiscoveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Platform_FullName",
                table: "Repositories",
                columns: new[] { "Platform", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Stars",
                table: "Repositories",
                column: "Stars");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Repositories");
        }
    }
}
