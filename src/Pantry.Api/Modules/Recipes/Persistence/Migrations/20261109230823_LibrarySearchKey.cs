using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Recipes.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LibrarySearchKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "search_key",
                schema: "recipes",
                table: "recipes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE recipes.recipes r
                SET search_key = translate(
                    lower(regexp_replace(btrim(r.name), '\s+', ' ', 'g')
                        || coalesce((SELECT string_agg(E'\n' || t.tag, '' ORDER BY t.tag) FROM recipes.recipe_tags t WHERE t.recipe_id = r.id), '')),
                    'ąćęłńóśźżäöüéèêáàâíìîúùûýçñ',
                    'acelnoszzaoueeeaaaiiiuuuycn');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "search_key",
                schema: "recipes",
                table: "recipes");
        }
    }
}
