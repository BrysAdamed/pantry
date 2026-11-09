using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Recipes.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "history_entries",
                schema: "recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    meal_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_history_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_history_entries_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalSchema: "recipes",
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_history_entries_plan_entry_id",
                schema: "recipes",
                table: "history_entries",
                column: "plan_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_history_entries_recipe_id_date",
                schema: "recipes",
                table: "history_entries",
                columns: new[] { "recipe_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history_entries",
                schema: "recipes");
        }
    }
}
