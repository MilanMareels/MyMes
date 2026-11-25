using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMES.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductversionDisc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VersionDescription",
                schema: "mes",
                table: "ProductVersions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VersionDescription",
                schema: "mes",
                table: "ProductVersions");
        }
    }
}
