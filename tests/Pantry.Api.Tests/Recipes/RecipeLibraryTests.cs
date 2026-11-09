using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipeLibraryTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private HttpClient _client = null!;
    private Guid _chicken;
    private Guid _peanuts;
    private Guid _rice;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
        _chicken = await factory.SeedIngredientAsync("Pierś z kurczaka", ["Filet drobiowy"], Unit.G);
        _peanuts = await factory.SeedIngredientAsync("Orzeszki ziemne", [], Unit.G);
        _rice = await factory.SeedIngredientAsync("Ryż", [], Unit.G);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Search_matches_the_name_ignoring_case_and_diacritics()
    {
        // Arrange
        var salmon = await CreateAsync("Łosoś z pieczonymi ziemniakami");
        await CreateAsync("Pomidorowa");

        // Act
        var page = await ListAsync("?search=LOSOS%20z");

        // Assert
        Assert.Equal([salmon], IdsOf(page));
        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Search_matches_a_tag()
    {
        // Arrange
        var pancakes = await CreateAsync("Naleśniki", tags: ["na słodko", "śniadanie"]);
        await CreateAsync("Pomidorowa", tags: ["obiad"]);

        // Act
        var page = await ListAsync("?search=sniad");

        // Assert
        Assert.Equal([pancakes], IdsOf(page));
    }

    [Fact]
    public async Task Search_matches_an_ingredient_name_or_alias()
    {
        // Arrange
        var curry = await CreateAsync("Curry", lines: [Line(_chicken), Line(_rice)]);
        var plainRice = await CreateAsync("Ryż na sypko", lines: [Line(_rice)]);
        await CreateAsync("Pomidorowa");

        // Act
        var byName = await ListAsync("?search=kurczak");
        var byAlias = await ListAsync("?search=drobiow");
        var byRice = await ListAsync("?search=ryz");

        // Assert
        Assert.Equal([curry], IdsOf(byName));
        Assert.Equal([curry], IdsOf(byAlias));
        Assert.Equal([curry, plainRice], IdsOf(byRice));
    }

    [Fact]
    public async Task Every_tag_given_must_be_on_the_recipe()
    {
        // Arrange
        var quickLunch = await CreateAsync("Leczo", tags: ["obiad", "szybkie"]);
        var lunch = await CreateAsync("Pierogi", tags: ["obiad"]);
        await CreateAsync("Owsianka", tags: ["śniadanie", "szybkie"]);

        // Act
        var lunches = await ListAsync("?tag=obiad");
        var quickLunches = await ListAsync("?tag=Obiad&tag=szybkie");

        // Assert
        Assert.Equal([quickLunch, lunch], IdsOf(lunches));
        Assert.Equal([quickLunch], IdsOf(quickLunches));
    }

    [Fact]
    public async Task Without_never_leaves_out_recipes_with_a_never_ingredient_but_keeps_avoid_ones()
    {
        // Arrange
        await AddDislikeAsync(_peanuts, "never");
        await AddDislikeAsync(_chicken, "avoid");
        var curry = await CreateAsync("Curry", lines: [Line(_chicken), Line(_rice)]);
        var satay = await CreateAsync("Satay", lines: [Line(_peanuts)]);

        // Act
        var all = await ListAsync();
        var withoutNever = await ListAsync("?withoutNever=true");

        // Assert
        Assert.Equal([curry, satay], IdsOf(all));
        Assert.Equal([curry], IdsOf(withoutNever));
        Assert.Equal(1, withoutNever.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filters_combine_with_archived_recipes()
    {
        // Arrange
        var oldSoup = await CreateAsync("Zupa grzybowa", tags: ["zupa"]);
        await CreateAsync("Zupa ogórkowa", tags: ["zupa"]);
        await _client.PostAsync($"{RecipesUrl}/{oldSoup}/archive", null);

        // Act
        var archived = await ListAsync("?archived=true&search=zupa&tag=zupa");

        // Assert
        Assert.Equal([oldSoup], IdsOf(archived));
    }

    [Fact]
    public async Task Rating_sort_puts_the_highest_average_first_and_unrated_last()
    {
        // Arrange
        var unrated = await CreateAsync("Agrest");
        var good = await CreateAsync("Bigos");
        var best = await CreateAsync("Curry");
        var popular = await CreateAsync("Dal");
        await RateAsync(good, new DateOnly(2026, 10, 1), 3);
        await RateAsync(best, new DateOnly(2026, 10, 2), 5);
        await RateAsync(popular, new DateOnly(2026, 10, 3), 4);
        await RateAsync(popular, new DateOnly(2026, 10, 4), 4);
        await RateAsync(good, new DateOnly(2026, 10, 5), 4);
        await factory.SeedHistoryEntryAsync(unrated, new DateOnly(2026, 10, 6));

        // Act
        var page = await ListAsync("?sort=rating");

        // Assert
        Assert.Equal([best, popular, good, unrated], IdsOf(page));
    }

    [Fact]
    public async Task Recently_eaten_sort_puts_the_latest_first_and_never_eaten_last()
    {
        // Arrange
        var neverEaten = await CreateAsync("Agrest");
        var longAgo = await CreateAsync("Bigos");
        var yesterday = await CreateAsync("Curry");
        await factory.SeedHistoryEntryAsync(longAgo, new DateOnly(2026, 9, 1));
        await factory.SeedHistoryEntryAsync(yesterday, new DateOnly(2026, 8, 1));
        await factory.SeedHistoryEntryAsync(yesterday, new DateOnly(2026, 11, 9));

        // Act
        var page = await ListAsync("?sort=recentlyEaten");

        // Assert
        Assert.Equal([yesterday, longAgo, neverEaten], IdsOf(page));
    }

    [Fact]
    public async Task Not_eaten_lately_sort_puts_never_eaten_first_then_the_longest_since_last_eaten()
    {
        // Arrange
        var yesterday = await CreateAsync("Agrest");
        var longAgo = await CreateAsync("Bigos");
        var neverEaten = await CreateAsync("Curry");
        await factory.SeedHistoryEntryAsync(longAgo, new DateOnly(2026, 9, 1));
        await factory.SeedHistoryEntryAsync(yesterday, new DateOnly(2026, 8, 1));
        await factory.SeedHistoryEntryAsync(yesterday, new DateOnly(2026, 11, 9));

        // Act
        var page = await ListAsync("?sort=notEatenLately");

        // Assert
        Assert.Equal([neverEaten, longAgo, yesterday], IdsOf(page));
    }

    [Fact]
    public async Task Prep_time_sort_puts_the_quickest_first_and_recipes_without_one_last()
    {
        // Arrange
        var noTime = await CreateAsync("Agrest");
        var slow = await CreateAsync("Bigos", prepTimeMinutes: 120);
        var quick = await CreateAsync("Curry", prepTimeMinutes: 20);

        // Act
        var page = await ListAsync("?sort=prepTime");

        // Assert
        Assert.Equal([quick, slow, noTime], IdsOf(page));
    }

    [Fact]
    public async Task Default_sort_is_by_name_and_pages_follow_the_sort()
    {
        // Arrange
        var c = await CreateAsync("curry", prepTimeMinutes: 10);
        var a = await CreateAsync("Agrest", prepTimeMinutes: 30);
        var b = await CreateAsync("Bigos", prepTimeMinutes: 20);

        // Act
        var byName = await ListAsync();
        var secondByTime = await ListAsync("?sort=prepTime&page=2&pageSize=2");

        // Assert
        Assert.Equal([a, b, c], IdsOf(byName));
        Assert.Equal([a], IdsOf(secondByTime));
        Assert.Equal(3, secondByTime.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Unknown_sort_is_rejected()
    {
        // Act
        var response = await _client.GetAsync($"{RecipesUrl}?sort=random");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("sort", out _));
    }

    private static object Line(Guid ingredientId) => new { ingredientId, quantity = 100, unit = "g" };

    private async Task<Guid> CreateAsync(string name, string[]? tags = null, object[]? lines = null, int? prepTimeMinutes = null)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2, tags = tags ?? [], lines = lines ?? [], prepTimeMinutes });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task RateAsync(Guid recipeId, DateOnly date, int stars)
    {
        var entry = await factory.SeedHistoryEntryAsync(recipeId, date);
        var response = await _client.PutAsJsonAsync($"{RecipesUrl}/history/{entry}/rating", new { stars });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AddDislikeAsync(Guid ingredientId, string kind)
    {
        var response = await _client.PostAsJsonAsync("/api/household/dislikes", new { ingredientId, kind });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<JsonElement> ListAsync(string query = "") => await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}{query}");

    private static List<Guid> IdsOf(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
}
