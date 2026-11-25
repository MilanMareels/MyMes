using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMES.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BomProductVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeLog",
                schema: "mes",
                table: "ProductVersions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveFrom",
                schema: "mes",
                table: "ProductVersions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveTo",
                schema: "mes",
                table: "ProductVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoutingSteps",
                schema: "mes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoutingSteps_ProductVersions_ProductVersionId",
                        column: x => x.ProductVersionId,
                        principalSchema: "mes",
                        principalTable: "ProductVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillOfMaterialItems",
                schema: "mes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RoutingStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillOfMaterialItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillOfMaterialItems_ProductVersions_ProductVersionId",
                        column: x => x.ProductVersionId,
                        principalSchema: "mes",
                        principalTable: "ProductVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillOfMaterialItems_Products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalSchema: "mes",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillOfMaterialItems_RoutingSteps_RoutingStepId",
                        column: x => x.RoutingStepId,
                        principalSchema: "mes",
                        principalTable: "RoutingSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterialItems_ComponentProductId",
                schema: "mes",
                table: "BillOfMaterialItems",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterialItems_ProductVersionId",
                schema: "mes",
                table: "BillOfMaterialItems",
                column: "ProductVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BillOfMaterialItems_RoutingStepId",
                schema: "mes",
                table: "BillOfMaterialItems",
                column: "RoutingStepId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingSteps_ProductVersionId_Sequence",
                schema: "mes",
                table: "RoutingSteps",
                columns: new[] { "ProductVersionId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillOfMaterialItems",
                schema: "mes");

            migrationBuilder.DropTable(
                name: "RoutingSteps",
                schema: "mes");

            migrationBuilder.DropColumn(
                name: "ChangeLog",
                schema: "mes",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                schema: "mes",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                schema: "mes",
                table: "ProductVersions");
        }
    }
}
