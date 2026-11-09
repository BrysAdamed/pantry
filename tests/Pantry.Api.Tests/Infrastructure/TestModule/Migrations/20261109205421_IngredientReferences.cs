using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Tests.Infrastructure.TestModule.Migrations
{
    /// <inheritdoc />
    public partial class IngredientReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingredient_references",
                schema: "test_module",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient_references", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_references_ingredient_id",
                schema: "test_module",
                table: "ingredient_references",
                column: "ingredient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingredient_references",
                schema: "test_module");
        }
    }
}
