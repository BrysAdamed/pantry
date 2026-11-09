using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipeCooldownTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private static readonly DateOnly LastEaten = new(2026, 11, 1);

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Never_eaten_recipe_is_available_now()
    {
        // Arrange
        var soup = await CreateRecipeAsync("Zupa pomidorowa", cooldownDays: null);

        // Act
        var cooldown = (await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{soup}")).GetProperty("cooldown");

        // Assert
        Assert.Equal(21, cooldown.GetProperty("days").GetInt32());
        Assert.True(cooldown.GetProperty("isHouseholdDefault").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cooldown.GetProperty("lastEaten").ValueKind);
        Assert.Equal(JsonValueKind.Null, cooldown.GetProperty("availableFrom").ValueKind);
    }

    [Fact]
    public async Task Available_from_is_the_last_eaten_date_plus_the_cooldown_in_effect()
    {
        // Arrange
        var soup = await CreateRecipeAsync("Zupa pomidorowa", cooldownDays: null);
        var pancakes = await CreateRecipeAsync("Naleśniki", cooldownDays: 7);
        var omelette = await CreateRecipeAsync("Omlet", cooldownDays: 0);
        foreach (var eaten in new[] { soup, pancakes, omelette })
        {
            await factory.SeedHistoryEntryAsync(eaten, LastEaten.AddDays(-10));
            await factory.SeedHistoryEntryAsync(eaten, LastEaten);
        }

        // Act
        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{pancakes}");
        var page = await _client.GetFromJsonAsync<JsonElement>(RecipesUrl);

        // Assert
        var detail = recipe.GetProperty("cooldown");
        Assert.Equal(7, detail.GetProperty("days").GetInt32());
        Assert.False(detail.GetProperty("isHouseholdDefault").GetBoolean());
        Assert.Equal("2026-11-01", detail.GetProperty("lastEaten").GetString());
        Assert.Equal("2026-11-08", detail.GetProperty("availableFrom").GetString());

        var items = page.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("cooldown"));
        Assert.Equal("2026-11-22", items[soup].GetProperty("availableFrom").GetString());
        Assert.Equal("2026-11-08", items[pancakes].GetProperty("availableFrom").GetString());
        Assert.Equal("2026-11-01", items[omelette].GetProperty("availableFrom").GetString());
    }

    [Fact]
    public async Task Contract_follows_the_household_cooldown_and_includes_archived_recipes()
    {
        // Arrange
        var soup = await CreateRecipeAsync("Zupa pomidorowa", cooldownDays: null);
        var pancakes = await CreateRecipeAsync("Naleśniki", cooldownDays: 7);
        var never = await CreateRecipeAsync("Bigos", cooldownDays: null);
        await factory.SeedHistoryEntryAsync(soup, LastEaten);
        await factory.SeedHistoryEntryAsync(pancakes, LastEaten);
        await _client.PostAsync($"{RecipesUrl}/{soup}/archive", null);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync("/api/household/cooldown", new { cooldownDays = 14 })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();

        // Act
        var cooldowns = (await scope.ServiceProvider.GetRequiredService<IRecipesModule>()
            .GetCooldownsAsync([soup, pancakes, never, Guid.NewGuid()])).ToDictionary(c => c.RecipeId);

        // Assert
        Assert.Equal(3, cooldowns.Count);
        Assert.Equal(14, cooldowns[soup].CooldownDays);
        Assert.Equal(new DateOnly(2026, 11, 15), cooldowns[soup].AvailableFrom);
        Assert.True(cooldowns[soup].IsTooSoonOn(new DateOnly(2026, 11, 14)));
        Assert.False(cooldowns[soup].IsTooSoonOn(new DateOnly(2026, 11, 15)));
        Assert.Equal(7, cooldowns[pancakes].CooldownDays);
        Assert.Equal(new DateOnly(2026, 11, 8), cooldowns[pancakes].AvailableFrom);
        Assert.Null(cooldowns[never].AvailableFrom);
        Assert.Null(cooldowns[never].LastEaten);
        Assert.False(cooldowns[never].IsTooSoonOn(LastEaten));
    }

    private async Task<Guid> CreateRecipeAsync(string name, int? cooldownDays)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2, cooldownDays });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
