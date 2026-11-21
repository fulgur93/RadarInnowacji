using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RadarInnowacji.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RepositoryNoiseAndDedup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalKey",
                table: "Repositories",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NoiseReason",
                table: "Repositories",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReadmeHash",
                table: "Repositories",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_CanonicalKey",
                table: "Repositories",
                column: "CanonicalKey");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_NoiseReason",
                table: "Repositories",
                column: "NoiseReason");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_ReadmeHash",
                table: "Repositories",
                column: "ReadmeHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Repositories_CanonicalKey",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_NoiseReason",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_ReadmeHash",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "CanonicalKey",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "NoiseReason",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "ReadmeHash",
                table: "Repositories");
        }
    }
}
