using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PossibleDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "not_duplicate_pairs",
                schema: "catalog",
                columns: table => new
                {
                    first_ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    second_ingredient_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_not_duplicate_pairs", x => new { x.first_ingredient_id, x.second_ingredient_id });
                    table.CheckConstraint("ck_not_duplicate_pairs_order", "first_ingredient_id < second_ingredient_id");
                    table.ForeignKey(
                        name: "fk_not_duplicate_pairs_ingredients_first_ingredient_id",
                        column: x => x.first_ingredient_id,
                        principalSchema: "catalog",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_not_duplicate_pairs_ingredients_second_ingredient_id",
                        column: x => x.second_ingredient_id,
                        principalSchema: "catalog",
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_names_key_trgm",
                schema: "catalog",
                table: "ingredient_names",
                column: "key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_not_duplicate_pairs_second_ingredient_id",
                schema: "catalog",
                table: "not_duplicate_pairs",
                column: "second_ingredient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "not_duplicate_pairs",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_ingredient_names_key_trgm",
                schema: "catalog",
                table: "ingredient_names");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
