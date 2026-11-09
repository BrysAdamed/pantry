using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class IngredientConversionsTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string IngredientsUrl = "/api/catalog/ingredients";

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_saves_conversions_in_unit_order()
    {
        // Act
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new
        {
            name = "Czosnek",
            baseUnit = "g",
            conversions = new[] { new { unit = "piece", amount = 50m }, new { unit = "clove", amount = 5.5m } },
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("piece", 50m), ("clove", 5.5m)], Conversions(created));
        var fetched = await _client.GetFromJsonAsync<JsonElement>(response.Headers.Location);
        Assert.Equal(Conversions(created), Conversions(fetched));
    }

    [Fact]
    public async Task Ingredient_without_conversions_has_an_empty_list()
    {
        var id = await CreateAsync("Mleko", "ml");

        var fetched = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{id}");

        Assert.Empty(Conversions(fetched));
    }

    [Fact]
    public async Task Update_adds_changes_and_removes_conversions()
    {
        // Arrange
        var id = await CreateAsync("Czosnek", "g", ("clove", 5m), ("piece", 50m));

        // Act
        var response = await UpdateAsync(id, new { unit = "clove", amount = 6m }, new { unit = "jar", amount = 120m });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("clove", 6m), ("jar", 120m)], Conversions(updated));
    }

    [Fact]
    public async Task Update_without_conversions_keeps_them()
    {
        var id = await CreateAsync("Czosnek", "g", ("clove", 5m));

        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Czosnek", category = "vegetablesAndFruit", isStaple = false });

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("clove", 5m)], Conversions(updated));
    }

    [Fact]
    public async Task Changing_the_base_unit_removes_the_conversions()
    {
        // Arrange
        var id = await CreateAsync("Czosnek", "g", ("clove", 5m));

        // Act
        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Czosnek", category = "other", isStaple = false, baseUnit = "piece" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("piece", updated.GetProperty("baseUnit").GetString());
        Assert.Empty(Conversions(updated));
    }

    [Fact]
    public async Task Conversions_sent_with_a_new_base_unit_are_in_that_unit()
    {
        var id = await CreateAsync("Czosnek", "g", ("clove", 5m));

        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new
        {
            name = "Czosnek",
            category = "other",
            isStaple = false,
            baseUnit = "piece",
            conversions = new[] { new { unit = "clove", amount = 0.1m } },
        });

        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("clove", 0.1m)], Conversions(updated));
    }

    [Theory]
    [InlineData("g", "clove", 0)]
    [InlineData("g", "clove", -5)]
    [InlineData("g", "clove", 0.00001)]
    [InlineData("g", "g", 1)]
    [InlineData("g", "kg", 1000)]
    [InlineData("ml", "tbsp", 15)]
    [InlineData("ml", "l", 1000)]
    [InlineData("piece", "piece", 1)]
    public async Task Invalid_or_redundant_conversions_are_rejected(string baseUnit, string unit, double amount)
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new
        {
            name = "Składnik",
            baseUnit,
            conversions = new[] { new { unit, amount = (decimal)amount } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Conversions", out _));
    }

    [Theory]
    [InlineData("""[{ "unit": "clove", "amount": 5 }, { "unit": "clove", "amount": 6 }]""")]
    [InlineData("""[{ "unit": "clove" }]""")]
    [InlineData("""[{ "amount": 5 }]""")]
    [InlineData("""[null]""")]
    [InlineData("""[{ "unit": "cup", "amount": 5 }]""")]
    [InlineData("""[{ "unit": 5, "amount": 5 }]""")]
    public async Task Malformed_conversions_are_rejected(string conversions)
    {
        var id = await CreateAsync("Czosnek", "g");
        var json = $$"""{ "name": "Czosnek", "category": "other", "isStaple": false, "conversions": {{conversions}} }""";

        var response = await _client.PutAsync($"{IngredientsUrl}/{id}", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_a_base_unit_that_is_not_one()
    {
        var id = await CreateAsync("Mąka", "g");

        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Mąka", category = "other", isStaple = false, baseUnit = "kg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Redundancy_is_checked_against_the_new_base_unit()
    {
        var id = await CreateAsync("Mąka", "g", ("tbsp", 10m));

        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new
        {
            name = "Mąka",
            category = "other",
            isStaple = false,
            baseUnit = "ml",
            conversions = new[] { new { unit = "tbsp", amount = 10m } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Contract_converts_into_the_base_unit_with_ingredient_conversions_first()
    {
        // Arrange
        var flour = await CreateAsync("Mąka", "g", ("tbsp", 10m), ("glass", 130m));
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();

        // Act
        var tbsp = await catalog.ConvertToBaseUnitAsync(flour, 2m, Unit.Tbsp);
        var kg = await catalog.ConvertToBaseUnitAsync(flour, 0.5m, Unit.Kg);
        var ml = await catalog.ConvertToBaseUnitAsync(flour, 100m, Unit.Ml);
        var unknown = await catalog.ConvertToBaseUnitAsync(Guid.NewGuid(), 1m, Unit.G);
        var ingredient = await catalog.GetIngredientAsync(flour);

        // Assert
        Assert.Equal(new BaseQuantity(Unit.G, 20m), tbsp);
        Assert.Equal(new BaseQuantity(Unit.G, 500m), kg);
        Assert.Equal(new BaseQuantity(Unit.G, null), ml);
        Assert.False(ml!.IsConverted);
        Assert.Null(unknown);
        Assert.Equal(130m, ingredient!.Conversions[Unit.Glass]);
    }

    [Fact]
    public async Task Contract_sums_quantities_with_the_stored_conversions()
    {
        // Arrange
        var garlic = await CreateAsync("Czosnek", "g", ("clove", 5m));
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();
        var unknown = Guid.NewGuid();

        // Act
        var totals = await catalog.SumQuantitiesAsync(
        [
            QuantityLine.Of(garlic, 2m, Unit.Clove),
            QuantityLine.Of(garlic, 0.01m, Unit.Kg),
            QuantityLine.Of(garlic, 1m, Unit.Jar),
            QuantityLine.ToTaste(unknown),
        ]);

        // Assert
        var total = Assert.Single(totals.Ingredients);
        Assert.Equal(20m, total.Total);
        Assert.Equal([new UnitQuantity(Unit.Jar, 1m)], total.OtherUnits);
        Assert.Equal([unknown], totals.UnknownIngredientIds);
    }

    [Fact]
    public async Task Merge_copies_the_conversions_the_target_lacks_when_base_units_match()
    {
        // Arrange
        var garlic = await CreateAsync("Czosnek", "g", ("clove", 5m));
        var youngGarlic = await CreateAsync("Czosnek młody", "g", ("clove", 8m), ("piece", 60m));

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{garlic}/merge-preview?sourceId={youngGarlic}");
        var response = await _client.PostAsJsonAsync($"{IngredientsUrl}/{garlic}/merge", new { sourceId = youngGarlic });

        // Assert
        Assert.Equal([("piece", 60m)], Conversions(preview, "conversionsToAdd"));
        Assert.Equal([("clove", 8m)], Conversions(preview, "conversionsSkipped"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var merged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("piece", 60m), ("clove", 5m)], Conversions(merged));
    }

    [Fact]
    public async Task Merge_skips_all_conversions_when_base_units_differ()
    {
        // Arrange
        var garlic = await CreateAsync("Czosnek", "piece", ("clove", 0.1m));
        var groundGarlic = await CreateAsync("Czosnek granulowany", "g", ("tsp", 3m));

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{garlic}/merge-preview?sourceId={groundGarlic}");
        var response = await _client.PostAsJsonAsync($"{IngredientsUrl}/{garlic}/merge", new { sourceId = groundGarlic });

        // Assert
        Assert.Empty(Conversions(preview, "conversionsToAdd"));
        Assert.Equal([("tsp", 3m)], Conversions(preview, "conversionsSkipped"));
        var merged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([("clove", 0.1m)], Conversions(merged));
    }

    private async Task<Guid> CreateAsync(string name, string baseUnit, params (string Unit, decimal Amount)[] conversions)
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new
        {
            name,
            baseUnit,
            conversions = conversions.Select(c => new { unit = c.Unit, amount = c.Amount }),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> UpdateAsync(Guid id, params object[] conversions) =>
        _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Czosnek", category = "other", isStaple = false, conversions });

    private static List<(string?, decimal)> Conversions(JsonElement element, string property = "conversions") =>
        element.GetProperty(property).EnumerateArray()
            .Select(c => (c.GetProperty("unit").GetString(), c.GetProperty("amount").GetDecimal()))
            .ToList();
}
