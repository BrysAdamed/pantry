using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipeHistoryTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private static readonly Guid Lunch = Guid.NewGuid();
    private static readonly Guid Dinner = Guid.NewGuid();

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
    public async Task History_requires_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var response = await anonymous.GetAsync($"{RecipesUrl}/{_pancakes}/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Eaten_plan_entry_becomes_a_history_entry_of_its_recipe()
    {
        // Arrange
        var planEntry = Guid.NewGuid();

        // Act
        await RecordEatenAsync(new EatenPlanEntry(planEntry, _pancakes, new DateOnly(2026, 11, 2), Lunch, IsLeftover: false));

        // Assert
        var history = await HistoryAsync(_pancakes);
        var entry = Assert.Single(history);
        Assert.Equal("2026-11-02", entry.GetProperty("date").GetString());
        Assert.Equal(Lunch, entry.GetProperty("mealSlotId").GetGuid());
        Assert.Equal(planEntry, entry.GetProperty("planEntryId").GetGuid());
        Assert.Equal(_pancakes, entry.GetProperty("recipeId").GetGuid());
        Assert.Empty(await HistoryAsync(_soup));
    }

    [Fact]
    public async Task Recording_the_same_plan_entry_twice_keeps_one_history_entry_with_the_latest_values()
    {
        // Arrange
        var planEntry = Guid.NewGuid();
        await RecordEatenAsync(new EatenPlanEntry(planEntry, _pancakes, new DateOnly(2026, 11, 2), Lunch, IsLeftover: false));

        // Act
        await RecordEatenAsync(new EatenPlanEntry(planEntry, _pancakes, new DateOnly(2026, 11, 2), Lunch, IsLeftover: false));
        await RecordEatenAsync(new EatenPlanEntry(planEntry, _soup, new DateOnly(2026, 11, 3), Dinner, IsLeftover: false));

        // Assert
        Assert.Empty(await HistoryAsync(_pancakes));
        var entry = Assert.Single(await HistoryAsync(_soup));
        Assert.Equal("2026-11-03", entry.GetProperty("date").GetString());
        Assert.Equal(Dinner, entry.GetProperty("mealSlotId").GetGuid());
    }

    [Fact]
    public async Task Uneaten_plan_entry_loses_its_history_entry_and_recording_it_again_does_nothing()
    {
        // Arrange
        var planEntry = Guid.NewGuid();
        var other = Guid.NewGuid();
        await RecordEatenAsync(new EatenPlanEntry(planEntry, _pancakes, new DateOnly(2026, 11, 2), Lunch, IsLeftover: false));
        await RecordEatenAsync(new EatenPlanEntry(other, _pancakes, new DateOnly(2026, 11, 5), Lunch, IsLeftover: false));

        // Act
        await RecordUneatenAsync(planEntry);
        await RecordUneatenAsync(planEntry);
        await RecordUneatenAsync(Guid.NewGuid());

        // Assert
        var entry = Assert.Single(await HistoryAsync(_pancakes));
        Assert.Equal(other, entry.GetProperty("planEntryId").GetGuid());
    }

    [Fact]
    public async Task Leftover_creates_no_history_entry_and_removes_one_recorded_before()
    {
        // Arrange
        var cooked = Guid.NewGuid();
        var leftover = Guid.NewGuid();
        await RecordEatenAsync(new EatenPlanEntry(cooked, _soup, new DateOnly(2026, 11, 2), Dinner, IsLeftover: false));

        // Act
        await RecordEatenAsync(new EatenPlanEntry(leftover, _soup, new DateOnly(2026, 11, 3), Lunch, IsLeftover: true));
        await RecordEatenAsync(new EatenPlanEntry(cooked, _soup, new DateOnly(2026, 11, 2), Dinner, IsLeftover: true));

        // Assert
        Assert.Empty(await HistoryAsync(_soup));
    }

    [Fact]
    public async Task Rolled_back_transaction_records_nothing()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await recipes.RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _soup, new DateOnly(2026, 11, 2), Dinner, IsLeftover: false), transaction);
            await transaction.RollbackAsync();
        }

        // Assert
        Assert.Empty(await HistoryAsync(_soup));
    }

    [Fact]
    public async Task Recording_an_unknown_recipe_is_rejected()
    {
        var entry = new EatenPlanEntry(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, 2), Dinner, IsLeftover: false);

        await Assert.ThrowsAsync<ArgumentException>(() => RecordEatenAsync(entry));
    }

    [Fact]
    public async Task History_is_newest_first_and_last_eaten_leaves_out_recipes_never_eaten()
    {
        // Arrange
        var never = await CreateRecipeAsync("Bigos");
        await RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _pancakes, new DateOnly(2026, 10, 20), Lunch, IsLeftover: false));
        await RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _pancakes, new DateOnly(2026, 11, 4), Lunch, IsLeftover: false));
        await RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _pancakes, new DateOnly(2026, 11, 1), Dinner, IsLeftover: false));
        await RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _soup, new DateOnly(2026, 9, 30), Dinner, IsLeftover: false));
        await using var scope = factory.Services.CreateAsyncScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        var history = await recipes.GetHistoryAsync(_pancakes);
        var lastEaten = await recipes.GetLastEatenAsync([_pancakes, _soup, never]);

        // Assert
        Assert.Equal([new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 20)], history.Select(h => h.Date));
        Assert.Equal(2, lastEaten.Count);
        Assert.Equal(new DateOnly(2026, 11, 4), lastEaten[_pancakes]);
        Assert.Equal(new DateOnly(2026, 9, 30), lastEaten[_soup]);
        Assert.False(lastEaten.ContainsKey(never));
    }

    [Fact]
    public async Task Archived_recipe_keeps_its_history_and_an_unknown_recipe_has_none()
    {
        // Arrange
        await RecordEatenAsync(new EatenPlanEntry(Guid.NewGuid(), _soup, new DateOnly(2026, 11, 2), Dinner, IsLeftover: false));

        // Act
        await _client.PostAsync($"{RecipesUrl}/{_soup}/archive", null);
        var unknown = await _client.GetAsync($"{RecipesUrl}/{Guid.NewGuid()}/history");

        // Assert
        Assert.Single(await HistoryAsync(_soup));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    private async Task<Guid> CreateRecipeAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<List<JsonElement>> HistoryAsync(Guid recipeId) =>
        (await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{recipeId}/history")).EnumerateArray().ToList();

    private Task RecordEatenAsync(EatenPlanEntry entry) =>
        InTransactionAsync((recipes, transaction) => recipes.RecordEatenAsync(entry, transaction));

    private Task RecordUneatenAsync(Guid planEntryId) =>
        InTransactionAsync((recipes, transaction) => recipes.RecordUneatenAsync(planEntryId, transaction));

    private async Task InTransactionAsync(Func<IRecipesModule, NpgsqlTransaction, Task> write)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        await write(scope.ServiceProvider.GetRequiredService<IRecipesModule>(), transaction);
        await transaction.CommitAsync();
    }
}
