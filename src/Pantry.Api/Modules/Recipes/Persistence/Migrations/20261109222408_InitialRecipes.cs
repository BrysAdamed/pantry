using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Recipes.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "recipes");

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    servings = table.Column<int>(type: "integer", nullable: false),
                    prep_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    source_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    make_again = table.Column<bool>(type: "boolean", nullable: false),
                    cooldown_days = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    carbs = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: true),
                    fat = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: true),
                    kcal = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: true),
                    protein = table.Column<decimal>(type: "numeric(9,1)", precision: 9, scale: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipes", x => x.id);
                    table.CheckConstraint("ck_recipes_cooldown_days", "cooldown_days IS NULL OR cooldown_days >= 0");
                    table.CheckConstraint("ck_recipes_prep_time_minutes", "prep_time_minutes IS NULL OR prep_time_minutes > 0");
                    table.CheckConstraint("ck_recipes_servings", "servings > 0");
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredient_lines",
                schema: "recipes",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    quantity_per_serving = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_ingredient_lines", x => new { x.recipe_id, x.position });
                    table.CheckConstraint("ck_recipe_ingredient_lines_quantity", "quantity IS NULL OR quantity > 0");
                    table.CheckConstraint("ck_recipe_ingredient_lines_unit", "(quantity IS NULL) = (unit IS NULL) AND (quantity IS NULL) = (quantity_per_serving IS NULL)");
                    table.ForeignKey(
                        name: "fk_recipe_ingredient_lines_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "recipes",
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_steps",
                schema: "recipes",
                columns: table => new
                {
                    position = table.Column<int>(type: "integer", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_steps", x => new { x.recipe_id, x.position });
                    table.ForeignKey(
                        name: "fk_recipe_steps_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "recipes",
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_tags",
                schema: "recipes",
                columns: table => new
                {
                    tag = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_tags", x => new { x.recipe_id, x.tag });
                    table.ForeignKey(
                        name: "fk_recipe_tags_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "recipes",
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredient_lines_ingredient_id",
                schema: "recipes",
                table: "recipe_ingredient_lines",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_tags_tag",
                schema: "recipes",
                table: "recipe_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_archived_at",
                schema: "recipes",
                table: "recipes",
                column: "archived_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_ingredient_lines",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "recipe_steps",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "recipe_tags",
                schema: "recipes");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "recipes");
        }
    }
}
