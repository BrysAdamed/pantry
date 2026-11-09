using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipeDislikeWarningsTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";
    private const string DislikesUrl = "/api/household/dislikes";

    private HttpClient _client = null!;
    private Guid _peanuts;
    private Guid _coriander;
    private Guid _rice;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
        _peanuts = await factory.SeedIngredientAsync("Orzeszki ziemne", [], Unit.G);
        _coriander = await factory.SeedIngredientAsync("Kolendra", [], Unit.G);
        _rice = await factory.SeedIngredientAsync("Ryż", [], Unit.G);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_and_detail_warn_about_never_ingredients_first_then_avoid_ones()
    {
        // Arrange
        await AddDislikeAsync(_coriander, "avoid");
        await AddDislikeAsync(_peanuts, "never", "alergia");
        var curry = await CreateAsync("Curry", new { ingredientId = _rice, quantity = 200, unit = "g" }, new { ingredientId = _coriander }, new { ingredientId = _peanuts, quantity = 50, unit = "g" });
        var plainRice = await CreateAsync("Ryż na sypko", new { ingredientId = _rice, quantity = 200, unit = "g" });

        // Act
        var detail = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{curry}");
        var list = await _client.GetFromJsonAsync<JsonElement>(RecipesUrl);

        // Assert
        List<(Guid, string?, string?, string?)> expected = [(_peanuts, "Orzeszki ziemne", "never", "alergia"), (_coriander, "Kolendra", "avoid", null)];
        Assert.Equal(expected, WarningsOf(detail));
        var items = list.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid());
        Assert.Equal(expected, WarningsOf(items[curry]));
        Assert.Empty(WarningsOf(items[plainRice]));
    }

    [Fact]
    public async Task Changing_a_dislike_changes_the_warnings_without_touching_the_recipe()
    {
        // Arrange
        var coriander = await AddDislikeAsync(_coriander, "avoid");
        var curry = await CreateAsync("Curry", new { ingredientId = _coriander }, new { ingredientId = _peanuts, quantity = 50, unit = "g" });
        var before = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{curry}");

        // Act
        var changed = await _client.PutAsJsonAsync($"{DislikesUrl}/{coriander}", new { kind = "never", reason = "smakuje jak mydło" });
        var peanuts = await AddDislikeAsync(_peanuts, "avoid");
        var afterChange = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{curry}");
        await _client.DeleteAsync($"{DislikesUrl}/{coriander}");
        await _client.DeleteAsync($"{DislikesUrl}/{peanuts}");
        var afterRemoval = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{curry}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal([(_coriander, "Kolendra", "avoid", (string?)null)], WarningsOf(before));
        Assert.Equal(
            [(_coriander, "Kolendra", "never", "smakuje jak mydło"), (_peanuts, "Orzeszki ziemne", "avoid", (string?)null)],
            WarningsOf(afterChange));
        Assert.Empty(WarningsOf(afterRemoval));
        Assert.Equal(before.GetProperty("updatedAt").GetDateTimeOffset(), afterRemoval.GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Saving_a_recipe_answers_with_its_warnings()
    {
        // Arrange
        await AddDislikeAsync(_peanuts, "never");

        // Act
        var response = await _client.PostAsJsonAsync(RecipesUrl, new
        {
            name = "Sałatka",
            servings = 2,
            sections = new object[] { new { name = "Posypka", lines = new object[] { new { ingredientId = _peanuts, quantity = 30, unit = "g" } } } },
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([(_peanuts, "Orzeszki ziemne", "never", (string?)null)], WarningsOf(await response.Content.ReadFromJsonAsync<JsonElement>()));
    }

    [Fact]
    public async Task Contract_reads_the_warnings_per_recipe_including_archived_recipes()
    {
        // Arrange
        await AddDislikeAsync(_coriander, "avoid");
        await AddDislikeAsync(_peanuts, "never", "alergia");
        var curry = await CreateAsync("Curry", new { ingredientId = _coriander }, new { ingredientId = _peanuts, quantity = 50, unit = "g" });
        var salad = await CreateAsync("Sałatka", new { ingredientId = _coriander, note = "do posypania" }, new { ingredientId = _coriander });
        var plainRice = await CreateAsync("Ryż na sypko", new { ingredientId = _rice, quantity = 200, unit = "g" });
        await _client.PostAsync($"{RecipesUrl}/{salad}/archive", null);
        await using var scope = factory.Services.CreateAsyncScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        var warnings = await recipes.GetDislikeWarningsAsync([curry, salad, plainRice, Guid.NewGuid()]);

        // Assert
        Assert.Equal(
            [
                new RecipeDislikeWarning(curry, _peanuts, DislikeKind.Never, "alergia"),
                new RecipeDislikeWarning(curry, _coriander, DislikeKind.Avoid, null),
            ],
            warnings.Where(w => w.RecipeId == curry));
        Assert.Equal([new RecipeDislikeWarning(salad, _coriander, DislikeKind.Avoid, null)], warnings.Where(w => w.RecipeId == salad));
        Assert.DoesNotContain(warnings, w => w.RecipeId == plainRice);
    }

    private async Task<Guid> AddDislikeAsync(Guid ingredientId, string kind, string? reason = null)
    {
        var response = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId, kind, reason });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateAsync(string name, params object[] lines)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2, lines });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static List<(Guid, string?, string?, string?)> WarningsOf(JsonElement recipe) =>
        recipe.GetProperty("dislikes").EnumerateArray()
            .Select(d => (
                d.GetProperty("ingredientId").GetGuid(),
                d.GetProperty("ingredientName").GetString(),
                d.GetProperty("kind").GetString(),
                d.GetProperty("reason").GetString()))
            .ToList();
}
