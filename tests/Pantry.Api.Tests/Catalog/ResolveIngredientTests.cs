using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class ResolveIngredientTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string IngredientsUrl = "/api/catalog/ingredients";
    private const string ResolveUrl = $"{IngredientsUrl}/resolve";

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Resolve_and_confirm_require_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var resolved = await anonymous.PostAsJsonAsync(ResolveUrl, new { name = "Cebula" });
        var confirmed = await anonymous.PostAsync($"{IngredientsUrl}/{Guid.NewGuid()}/confirm", null);

        Assert.Equal(HttpStatusCode.Unauthorized, resolved.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, confirmed.StatusCode);
    }

    [Theory]
    [InlineData("ser żółty")]
    [InlineData("  SER   ZOLTY ")]
    [InlineData("gouda")]
    public async Task Known_name_or_alias_resolves_to_the_existing_ingredient(string name)
    {
        // Arrange
        var cheese = await CreateAsync("Ser żółty", ["Gouda"]);

        // Act
        var response = await _client.PostAsJsonAsync(ResolveUrl, new { name, unit = "kg" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resolved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(cheese, resolved.GetProperty("id").GetGuid());
        Assert.False(resolved.GetProperty("needsReview").GetBoolean());
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Unknown_name_creates_an_ingredient_that_needs_review()
    {
        // Act
        var response = await _client.PostAsJsonAsync(ResolveUrl, new { name = "  Mąka  pszenna ", unit = "tbsp" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mąka pszenna", created.GetProperty("name").GetString());
        Assert.Empty(created.GetProperty("aliases").EnumerateArray());
        Assert.Equal("ml", created.GetProperty("baseUnit").GetString());
        Assert.Equal("other", created.GetProperty("category").GetString());
        Assert.False(created.GetProperty("isStaple").GetBoolean());
        Assert.True(created.GetProperty("needsReview").GetBoolean());

        var needsReview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?needsReview=true");
        Assert.Equal(created.GetProperty("id").GetGuid(), Assert.Single(needsReview.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Unknown_name_without_a_unit_is_counted_in_pieces()
    {
        var response = await _client.PostAsJsonAsync(ResolveUrl, new { name = "Por" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("piece", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("baseUnit").GetString());
    }

    [Fact]
    public async Task Resolving_the_same_new_name_twice_creates_one_ingredient()
    {
        var first = await _client.PostAsJsonAsync(ResolveUrl, new { name = "Szczypiorek", unit = "bunch" });
        var second = await _client.PostAsJsonAsync(ResolveUrl, new { name = "szczypiorek", unit = "g" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await IdOf(first), await IdOf(second));
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Parallel_resolves_of_the_same_new_name_create_one_ingredient()
    {
        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            _client.PostAsJsonAsync(ResolveUrl, new { name = i % 2 == 0 ? "Koperek" : "KOPEREK", unit = "g" })));

        // Assert
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var ids = await Task.WhenAll(responses.Select(IdOf));
        Assert.Single(ids.Distinct());
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Parallel_contract_resolves_of_the_same_new_name_create_one_ingredient()
    {
        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICatalogModule>().ResolveIngredientAsync("Natka pietruszki", Unit.Bunch);
        }));

        // Assert
        Assert.Single(results, r => r.Created);
        Assert.Single(results.Select(r => r.Ingredient.Id).Distinct());
        Assert.All(results, r => Assert.Equal(Unit.Piece, r.Ingredient.BaseUnit));
        Assert.All(results, r => Assert.True(r.Ingredient.NeedsReview));
    }

    [Theory]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "   " }""")]
    [InlineData("""{ }""")]
    [InlineData("""{ "name": "Mąka", "unit": "cup" }""")]
    [InlineData("""{ "name": "Mąka", "unit": 1 }""")]
    public async Task Invalid_resolves_are_rejected(string json)
    {
        var response = await _client.PostAsync(ResolveUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Too_long_name_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(ResolveUrl, new { name = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_clears_needs_review()
    {
        // Arrange
        var id = await IdOf(await _client.PostAsJsonAsync(ResolveUrl, new { name = "Imbir", unit = "g" }));

        // Act
        var response = await _client.PostAsync($"{IngredientsUrl}/{id}/confirm", null);
        var again = await _client.PostAsync($"{IngredientsUrl}/{id}/confirm", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("needsReview").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{id}");
        Assert.False(fetched.GetProperty("needsReview").GetBoolean());
        Assert.Equal("g", fetched.GetProperty("baseUnit").GetString());
    }

    [Fact]
    public async Task Confirm_of_an_unknown_ingredient_returns_404()
    {
        var response = await _client.PostAsync($"{IngredientsUrl}/{Guid.NewGuid()}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Contract_resolves_and_confirms()
    {
        // Arrange
        var cheese = await CreateAsync("Ser żółty", ["Gouda"]);
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();

        // Act
        var known = await catalog.ResolveIngredientAsync("GOUDA", Unit.G);
        var unknown = await catalog.ResolveIngredientAsync("Oliwa", Unit.L);
        var confirmed = await catalog.ConfirmIngredientAsync(unknown.Ingredient.Id);
        var missing = await catalog.ConfirmIngredientAsync(Guid.NewGuid());

        // Assert
        Assert.False(known.Created);
        Assert.Equal(cheese, known.Ingredient.Id);
        Assert.True(unknown.Created);
        Assert.Equal(Unit.Ml, unknown.Ingredient.BaseUnit);
        Assert.True(unknown.Ingredient.NeedsReview);
        Assert.False(confirmed?.NeedsReview);
        Assert.Null(missing);
        await Assert.ThrowsAsync<ArgumentException>(() => catalog.ResolveIngredientAsync("  ", null));
    }

    private async Task<Guid> CreateAsync(string name, string[] aliases)
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name, aliases, baseUnit = "g" });
        response.EnsureSuccessStatusCode();
        return await IdOf(response);
    }

    private async Task<int> CountAsync() =>
        (await _client.GetFromJsonAsync<JsonElement>(IngredientsUrl)).GetProperty("total").GetInt32();

    private static async Task<Guid> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
}
