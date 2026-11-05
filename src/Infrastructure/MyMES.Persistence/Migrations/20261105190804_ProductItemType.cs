using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMES.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductItemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "mes",
                table: "ProductVersions");

            migrationBuilder.AddColumn<double>(
                name: "Quantity",
                schema: "mes",
                table: "ProductVersions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "VersionDescription",
                schema: "mes",
                table: "ProductVersions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                schema: "mes",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "mes",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "VersionDescription",
                schema: "mes",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "ItemType",
                schema: "mes",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "mes",
                table: "ProductVersions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
