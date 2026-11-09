using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngredientUnitConversions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingredient_conversions",
                schema: "catalog",
                columns: table => new
                {
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient_conversions", x => new { x.ingredient_id, x.unit });
                    table.CheckConstraint("ck_ingredient_conversions_amount", "amount > 0");
                    table.ForeignKey(
                        name: "fk_ingredient_conversions_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalSchema: "catalog",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingredient_conversions",
                schema: "catalog");
        }
    }
}
