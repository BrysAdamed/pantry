using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RatingsTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private HttpClient _client = null!;
    private Guid _pancakes;
    private Guid _soup;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
        _pancakes = await CreateRecipeAsync("Naleśniki");
        _soup = await CreateRecipeAsync("Zupa pomidorowa");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ratings_require_a_session()
    {
        var anonymous = factory.CreateBrowserClient();
        var entry = await factory.SeedHistoryEntryAsync(_pancakes, Today);

        var rate = await anonymous.PutAsJsonAsync(RatingUrl(entry), new { stars = 5 });
        var remove = await anonymous.DeleteAsync(RatingUrl(entry));
        var toRate = await anonymous.GetAsync($"{RecipesUrl}/history/to-rate");
        var makeAgain = await anonymous.PutAsJsonAsync($"{RecipesUrl}/{_pancakes}/make-again", new { makeAgain = false });

        Assert.Equal(HttpStatusCode.Unauthorized, rate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, toRate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, makeAgain.StatusCode);
    }

    [Fact]
    public async Task Rating_history_entries_gives_the_recipe_an_average_and_a_count()
    {
        // Arrange
        var first = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(-3));
        var second = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(-1));
        await factory.SeedHistoryEntryAsync(_pancakes, Today);

        // Act
        await RateAsync(first, new { stars = 3 });
        await RateAsync(second, new { stars = 4 });
        var changed = await RateAsync(first, new { stars = 5 });

        // Assert
        Assert.Equal(5, changed.GetProperty("entry").GetProperty("rating").GetInt32());
        Assert.Equal(4.5m, changed.GetProperty("recipe").GetProperty("averageRating").GetDecimal());
        Assert.Equal(2, changed.GetProperty("recipe").GetProperty("ratingCount").GetInt32());
        Assert.True(changed.GetProperty("recipe").GetProperty("isFavourite").GetBoolean());

        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{_pancakes}");
        Assert.Equal(4.5m, recipe.GetProperty("averageRating").GetDecimal());
        Assert.Equal(2, recipe.GetProperty("ratingCount").GetInt32());
        Assert.True(recipe.GetProperty("isFavourite").GetBoolean());

        var history = (await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{_pancakes}/history")).EnumerateArray().ToList();
        Assert.Equal([null, 4, 5], history.Select(h => h.GetProperty("rating").ValueKind == JsonValueKind.Null ? (int?)null : h.GetProperty("rating").GetInt32()));
    }

    [Fact]
    public async Task Library_lists_the_average_rating_count_and_favourite()
    {
        // Arrange
        await RateAsync(await factory.SeedHistoryEntryAsync(_soup, Today), new { stars = 2 });

        // Act
        var page = await _client.GetFromJsonAsync<JsonElement>(RecipesUrl);

        // Assert
        var items = page.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, items[_pancakes].GetProperty("averageRating").ValueKind);
        Assert.Equal(0, items[_pancakes].GetProperty("ratingCount").GetInt32());
        Assert.Equal(2m, items[_soup].GetProperty("averageRating").GetDecimal());
        Assert.Equal(1, items[_soup].GetProperty("ratingCount").GetInt32());
        Assert.False(items[_soup].GetProperty("isFavourite").GetBoolean());
    }

    [Fact]
    public async Task Make_again_answer_of_the_latest_rating_wins_and_no_answer_keeps_it()
    {
        // Arrange
        var first = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(-2));
        var second = await factory.SeedHistoryEntryAsync(_pancakes, Today);

        // Act
        var no = await RateAsync(first, new { stars = 5, makeAgain = false });
        var unanswered = await RateAsync(second, new { stars = 5 });
        var yes = await RateAsync(first, new { stars = 5, makeAgain = true });

        // Assert
        Assert.False(no.GetProperty("recipe").GetProperty("makeAgain").GetBoolean());
        Assert.False(no.GetProperty("recipe").GetProperty("isFavourite").GetBoolean());
        Assert.False(unanswered.GetProperty("recipe").GetProperty("makeAgain").GetBoolean());
        Assert.True(yes.GetProperty("recipe").GetProperty("makeAgain").GetBoolean());
        Assert.True(yes.GetProperty("recipe").GetProperty("isFavourite").GetBoolean());
        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{_pancakes}");
        Assert.True(recipe.GetProperty("makeAgain").GetBoolean());
    }

    [Fact]
    public async Task Make_again_can_be_changed_on_the_recipe()
    {
        // Arrange
        await RateAsync(await factory.SeedHistoryEntryAsync(_pancakes, Today), new { stars = 5 });

        // Act
        var response = await _client.PutAsJsonAsync($"{RecipesUrl}/{_pancakes}/make-again", new { makeAgain = false });
        var missing = await _client.PutAsJsonAsync($"{RecipesUrl}/{_pancakes}/make-again", new { });
        var unknown = await _client.PutAsJsonAsync($"{RecipesUrl}/{Guid.NewGuid()}/make-again", new { makeAgain = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rating = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(rating.GetProperty("makeAgain").GetBoolean());
        Assert.False(rating.GetProperty("isFavourite").GetBoolean());
        Assert.Equal(5m, rating.GetProperty("averageRating").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{_pancakes}");
        Assert.False(recipe.GetProperty("makeAgain").GetBoolean());
    }

    [Fact]
    public async Task Removed_rating_leaves_the_average_and_keeps_make_again()
    {
        // Arrange
        var entry = await factory.SeedHistoryEntryAsync(_pancakes, Today);
        await RateAsync(entry, new { stars = 2, makeAgain = false });

        // Act
        var response = await _client.DeleteAsync(RatingUrl(entry));
        var unknown = await _client.DeleteAsync(RatingUrl(Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var removed = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("entry").GetProperty("rating").ValueKind);
        Assert.Equal(JsonValueKind.Null, removed.GetProperty("recipe").GetProperty("averageRating").ValueKind);
        Assert.Equal(0, removed.GetProperty("recipe").GetProperty("ratingCount").GetInt32());
        Assert.False(removed.GetProperty("recipe").GetProperty("makeAgain").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains(entry, await ToRateIdsAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(null)]
    public async Task Rating_outside_one_to_five_stars_is_rejected(int? stars)
    {
        var entry = await factory.SeedHistoryEntryAsync(_pancakes, Today);

        var response = await _client.PutAsJsonAsync(RatingUrl(entry), new { stars });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("stars", out _));
    }

    [Fact]
    public async Task Rating_an_unknown_history_entry_is_not_found()
    {
        var response = await _client.PutAsJsonAsync(RatingUrl(Guid.NewGuid()), new { stars = 4 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task History_entry_of_an_archived_recipe_can_be_rated()
    {
        // Arrange
        var entry = await factory.SeedHistoryEntryAsync(_soup, Today);
        await _client.PostAsync($"{RecipesUrl}/{_soup}/archive", null);

        // Act
        var response = await _client.PutAsJsonAsync(RatingUrl(entry), new { stars = 4, makeAgain = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, rated.GetProperty("entry").GetProperty("rating").GetInt32());
        Assert.False(rated.GetProperty("recipe").GetProperty("makeAgain").GetBoolean());
    }

    [Fact]
    public async Task To_rate_lists_unrated_entries_of_the_last_fourteen_days_newest_first()
    {
        // Arrange
        await _client.PostAsync($"{RecipesUrl}/{_soup}/archive", null);
        var today = await factory.SeedHistoryEntryAsync(_pancakes, Today);
        var oldest = await factory.SeedHistoryEntryAsync(_soup, Today.AddDays(-13));
        var tooOld = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(-14));
        var rated = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(-1));
        var future = await factory.SeedHistoryEntryAsync(_pancakes, Today.AddDays(1));
        await RateAsync(rated, new { stars = 4 });

        // Act
        var items = (await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/history/to-rate")).EnumerateArray().ToList();
        var lastThreeDays = await ToRateIdsAsync("?days=3");
        var invalid = await _client.GetAsync($"{RecipesUrl}/history/to-rate?days=0");

        // Assert
        Assert.Equal([today, oldest], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal("Naleśniki", items[0].GetProperty("recipeName").GetString());
        Assert.False(items[0].GetProperty("recipeIsArchived").GetBoolean());
        Assert.True(items[1].GetProperty("recipeIsArchived").GetBoolean());
        Assert.Equal(Today.AddDays(-13).ToString("yyyy-MM-dd"), items[1].GetProperty("date").GetString());
        Assert.DoesNotContain(tooOld, lastThreeDays);
        Assert.DoesNotContain(future, lastThreeDays);
        Assert.Equal([today], lastThreeDays);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Uneaten_plan_entry_takes_its_rating_with_it_and_another_recipe_drops_the_rating()
    {
        // Arrange
        var planEntry = Guid.NewGuid();
        var moved = Guid.NewGuid();
        await factory.RecordEatenAsync(new EatenPlanEntry(planEntry, _pancakes, Today, Guid.NewGuid(), IsLeftover: false));
        await factory.RecordEatenAsync(new EatenPlanEntry(moved, _pancakes, Today, Guid.NewGuid(), IsLeftover: false));
        foreach (var entry in await HistoryAsync(_pancakes))
        {
            await RateAsync(entry.Id, new { stars = 5 });
        }

        // Act
        await factory.RecordUneatenAsync(planEntry);
        await factory.RecordEatenAsync(new EatenPlanEntry(moved, _soup, Today, Guid.NewGuid(), IsLeftover: false));

        // Assert
        await using var scope = factory.Services.CreateAsyncScope();
        var ratings = await scope.ServiceProvider.GetRequiredService<IRecipesModule>().GetRatingsAsync([_pancakes, _soup]);
        Assert.All(ratings, r => Assert.Equal(0, r.RatingCount));
        Assert.Null(Assert.Single(await HistoryAsync(_soup)).Rating);
    }

    [Fact]
    public async Task Contract_gives_ratings_and_favourites_that_are_not_archived()
    {
        // Arrange
        var bigos = await CreateRecipeAsync("Bigos");
        var curry = await CreateRecipeAsync("Curry");
        await RateAsync(await factory.SeedHistoryEntryAsync(_pancakes, Today), new { stars = 4 });
        await RateAsync(await factory.SeedHistoryEntryAsync(_soup, Today), new { stars = 5 });
        await RateAsync(await factory.SeedHistoryEntryAsync(bigos, Today), new { stars = 5, makeAgain = false });
        await RateAsync(await factory.SeedHistoryEntryAsync(curry, Today), new { stars = 3 });
        await _client.PostAsync($"{RecipesUrl}/{_soup}/archive", null);
        await using var scope = factory.Services.CreateAsyncScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        var favourites = await recipes.GetFavouritesAsync();
        var ratings = (await recipes.GetRatingsAsync([_pancakes, _soup, bigos, curry, Guid.NewGuid()])).ToDictionary(r => r.RecipeId);

        // Assert
        Assert.Equal([_pancakes], favourites.Select(f => f.Id));
        Assert.Equal(4, ratings.Count);
        Assert.True(ratings[_pancakes].IsFavourite);
        Assert.True(ratings[_soup].IsFavourite);
        Assert.False(ratings[bigos].IsFavourite);
        Assert.False(ratings[bigos].MakeAgain);
        Assert.Equal(3m, ratings[curry].AverageRating);
        Assert.False(ratings[curry].IsFavourite);
    }

    private static string RatingUrl(Guid entryId) => $"{RecipesUrl}/history/{entryId}/rating";

    private async Task<JsonElement> RateAsync(Guid entryId, object request)
    {
        var response = await _client.PutAsJsonAsync(RatingUrl(entryId), request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<Guid>> ToRateIdsAsync(string query = "") =>
        (await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/history/to-rate{query}"))
            .EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private async Task<IReadOnlyList<RecipeHistoryEntry>> HistoryAsync(Guid recipeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRecipesModule>().GetHistoryAsync(recipeId);
    }

    private async Task<Guid> CreateRecipeAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
