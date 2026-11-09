using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipeIngredientReferencesTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";
    private const string IngredientsUrl = "/api/catalog/ingredients";

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_ingredient_used_in_recipes_cannot_be_deleted_and_lines_are_counted()
    {
        // Arrange
        var flour = await factory.SeedIngredientAsync("Flour", [], Unit.G);
        await CreateAsync("Pancakes", [Line(flour, 250, "g"), Line(flour, 20, "g")]);
        var archived = await CreateAsync("Bread", [Line(flour, 500, "g")]);
        (await _client.PostAsync($"{RecipesUrl}/{archived}/archive", null)).EnsureSuccessStatusCode();

        // Act
        var response = await _client.DeleteAsync($"{IngredientsUrl}/{flour}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var references = Assert.Single(problem.GetProperty("references").EnumerateArray());
        Assert.Equal("recipes", references.GetProperty("module").GetString());
        Assert.Equal(3, references.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Merge_moves_recipe_lines_to_the_target_and_keeps_both_lines_of_a_recipe_that_had_both()
    {
        // Arrange
        var target = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var source = await factory.SeedIngredientAsync("Cilantro", [], Unit.G);
        var salt = await factory.SeedIngredientAsync("Salt", [], Unit.G);
        var salsa = await CreateAsync("Salsa", [Line(source, 10, "g", "chopped"), Line(salt, null, null)]);
        var curry = await CreateAsync("Curry", [Line(target, 5, "g"), Line(source, 15, "g")]);

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{target}/merge-preview?sourceId={source}");
        var merged = await _client.PostAsJsonAsync($"{IngredientsUrl}/{target}/merge", new { sourceId = source });

        // Assert
        merged.EnsureSuccessStatusCode();
        var recipes = Assert.Single(preview.GetProperty("references").EnumerateArray(), r => r.GetProperty("module").GetString() == "recipes");
        Assert.Equal((2, 1), (recipes.GetProperty("source").GetInt32(), recipes.GetProperty("target").GetInt32()));
        Assert.Equal([(target, 10m, "chopped"), (salt, (decimal?)null, null)], await LinesAsync(salsa));
        Assert.Equal([(target, 5m, null), (target, 15m, null)], await LinesAsync(curry));
    }

    private static object Line(Guid ingredientId, decimal? quantity, string? unit, string? note = null) =>
        new { ingredientId, quantity, unit, note };

    private async Task<Guid> CreateAsync(string name, object[] lines)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2, lines });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<List<(Guid, decimal?, string?)>> LinesAsync(Guid recipeId)
    {
        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{recipeId}");
        return recipe.GetProperty("lines").EnumerateArray()
            .Select(l => (
                l.GetProperty("ingredientId").GetGuid(),
                l.GetProperty("quantity").ValueKind == JsonValueKind.Null ? (decimal?)null : l.GetProperty("quantity").GetDecimal(),
                l.GetProperty("note").GetString()))
            .ToList();
    }
}
