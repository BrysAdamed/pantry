using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class IngredientsTests(PantryApiFactory factory) : IAsyncLifetime
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
    public async Task Catalog_requires_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var list = await anonymous.GetAsync(IngredientsUrl);
        var vocabulary = await anonymous.GetAsync("/api/catalog/vocabulary");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, vocabulary.StatusCode);
    }

    [Fact]
    public async Task Created_ingredient_is_cleaned_defaults_to_other_and_does_not_need_review()
    {
        // Act
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name = "  Cebula  ", aliases = new[] { "Cebulka " }, baseUnit = "piece" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cebula", created.GetProperty("name").GetString());
        Assert.Equal(["Cebulka"], created.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("piece", created.GetProperty("baseUnit").GetString());
        Assert.Equal("other", created.GetProperty("category").GetString());
        Assert.False(created.GetProperty("isStaple").GetBoolean());
        Assert.False(created.GetProperty("needsReview").GetBoolean());

        var fetched = await _client.GetFromJsonAsync<JsonElement>(response.Headers.Location);
        Assert.Equal(created.GetProperty("id").GetGuid(), fetched.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("cebula")]
    [InlineData("CEBULA")]
    [InlineData(" Cebula ")]
    [InlineData("cebulka")]
    public async Task Name_already_used_as_a_name_or_alias_is_rejected(string name)
    {
        // Arrange
        var onion = await CreateAsync("Cebula", ["Cebulka"]);

        // Act
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name, baseUnit = "piece" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var conflict = Assert.Single(problem.GetProperty("conflicts").EnumerateArray());
        Assert.Equal(onion, conflict.GetProperty("ingredientId").GetGuid());
        Assert.Equal("Cebula", conflict.GetProperty("ingredientName").GetString());
    }

    [Fact]
    public async Task Alias_that_matches_another_name_without_diacritics_is_rejected()
    {
        await CreateAsync("Ser żółty", []);

        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name = "Gouda", aliases = new[] { "ser zolty" }, baseUnit = "g" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Cebula", "cebula")]
    [InlineData("Żółty ser", "zolty ser")]
    [InlineData("Cebula", "")]
    public async Task Aliases_must_be_valid_and_differ_from_the_name_and_each_other(string name, string alias)
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name, aliases = new[] { alias }, baseUnit = "piece" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Aliases", out _));
    }

    [Theory]
    [InlineData("""{ "name": "", "baseUnit": "g" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "kg" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "tbsp" }""")]
    [InlineData("""{ "name": "Mąka" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "cup" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": 0 }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "0" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "g", "category": "sweets" }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "g", "category": 4 }""")]
    [InlineData("""{ "name": "Mąka", "baseUnit": "g", "category": "4" }""")]
    public async Task Invalid_ingredients_are_rejected(string json)
    {
        var response = await _client.PostAsync(IngredientsUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_renames_replaces_aliases_and_sets_category_and_staple()
    {
        // Arrange
        var id = await CreateAsync("Cebula", ["Cebulka"]);

        // Act
        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new
        {
            name = "Cebulka",
            aliases = new[] { "cebula", "Szalotka" },
            category = "vegetablesAndFruit",
            isStaple = true,
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cebulka", updated.GetProperty("name").GetString());
        Assert.Equal(["cebula", "Szalotka"], updated.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("vegetablesAndFruit", updated.GetProperty("category").GetString());
        Assert.True(updated.GetProperty("isStaple").GetBoolean());
        Assert.Equal("piece", updated.GetProperty("baseUnit").GetString());
    }

    [Fact]
    public async Task Update_cannot_take_a_name_of_another_ingredient()
    {
        await CreateAsync("Cebula", []);
        var garlic = await CreateAsync("Czosnek", []);

        var response = await _client.PutAsJsonAsync($"{IngredientsUrl}/{garlic}", new
        {
            name = "Czosnek",
            aliases = new[] { "CEBULA" },
            category = "vegetablesAndFruit",
            isStaple = false,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_requires_a_known_category()
    {
        var id = await CreateAsync("Cebula", []);

        var missing = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Cebula", isStaple = false });
        var unknown = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Cebula", category = "sweets", isStaple = false });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Unknown_ingredient_returns_404()
    {
        var id = Guid.NewGuid();

        var fetched = await _client.GetAsync($"{IngredientsUrl}/{id}");
        var updated = await _client.PutAsJsonAsync($"{IngredientsUrl}/{id}", new { name = "Cebula", category = "other", isStaple = false });

        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
    }

    [Fact]
    public async Task List_is_in_name_order_ignoring_diacritics_and_pages()
    {
        // Arrange
        foreach (var name in new[] { "Marchew", "Ćwikła", "Jabłko", "Czosnek", "Banan" })
        {
            await CreateAsync(name, []);
        }

        // Act
        var first = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?pageSize=3");
        var second = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?pageSize=3&page=2");

        // Assert
        Assert.Equal(5, first.GetProperty("total").GetInt32());
        Assert.Equal(["Banan", "Ćwikła", "Czosnek"], Names(first));
        Assert.Equal(["Jabłko", "Marchew"], Names(second));
    }

    [Fact]
    public async Task List_filters_by_category_staple_and_needs_review()
    {
        // Arrange
        await CreateAsync("Sól", [], category: "spices", isStaple: true);
        await CreateAsync("Pieprz", [], category: "spices");
        await CreateAsync("Mleko", [], category: "dairyAndEggs");

        // Act
        var spices = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?category=spices");
        var staples = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?staple=true");
        var notStapleSpices = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?category=spices&staple=false");
        var needsReview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?needsReview=true");

        // Assert
        Assert.Equal(["Pieprz", "Sól"], Names(spices));
        Assert.Equal(["Sól"], Names(staples));
        Assert.Equal(["Pieprz"], Names(notStapleSpices));
        Assert.Empty(Names(needsReview));
    }

    [Theory]
    [InlineData("category=sweets")]
    [InlineData("category=7")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    [InlineData("staple=maybe")]
    public async Task Invalid_list_queries_are_rejected(string query)
    {
        var response = await _client.GetAsync($"{IngredientsUrl}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_matches_names_and_aliases_without_diacritics_with_prefix_matches_first()
    {
        // Arrange
        await CreateAsync("Ser żółty", ["Gouda"]);
        await CreateAsync("Żółtko", []);
        await CreateAsync("Mleko", []);

        // Act
        var byName = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?search=zolt");
        var byAlias = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?search=GOUD");
        var percent = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}?search=%25");

        // Assert
        Assert.Equal(["Żółtko", "Ser żółty"], Names(byName));
        Assert.Equal(["Ser żółty"], Names(byAlias));
        Assert.Empty(Names(percent));
    }

    [Fact]
    public async Task Vocabulary_lists_categories_and_units_in_their_fixed_order()
    {
        var vocabulary = await _client.GetFromJsonAsync<JsonElement>("/api/catalog/vocabulary");

        Assert.Equal(IngredientCategories.Ordered.Select(CatalogCode.Of), vocabulary.GetProperty("categories").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("other", vocabulary.GetProperty("defaultCategory").GetString());
        Assert.Equal(["g", "ml", "piece"], vocabulary.GetProperty("baseUnits").EnumerateArray().Select(u => u.GetString()));
        var units = vocabulary.GetProperty("units").EnumerateArray().ToList();
        Assert.Equal(Units.Ordered.Select(CatalogCode.Of), units.Select(u => u.GetProperty("code").GetString()));
        var tbsp = units.Single(u => u.GetProperty("code").GetString() == "tbsp").GetProperty("genericConversion");
        Assert.Equal("ml", tbsp.GetProperty("baseUnit").GetString());
        Assert.Equal(15m, tbsp.GetProperty("factor").GetDecimal());
        Assert.Equal(JsonValueKind.Null, units.Single(u => u.GetProperty("code").GetString() == "clove").GetProperty("genericConversion").ValueKind);
    }

    [Fact]
    public async Task Contract_finds_ingredients_by_name_or_alias()
    {
        // Arrange
        var cheese = await CreateAsync("Ser żółty", ["Gouda"], category: "dairyAndEggs", baseUnit: "g");
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();

        // Act
        var byName = await catalog.FindIngredientAsync("  ser   ZOLTY ");
        var byAlias = await catalog.FindIngredientAsync("gouda");
        var missing = await catalog.FindIngredientAsync("Edam");
        var byIds = await catalog.GetIngredientsAsync([cheese, Guid.NewGuid()]);

        // Assert
        Assert.Equal(cheese, byName?.Id);
        Assert.Equal(IngredientCategory.DairyAndEggs, byName?.Category);
        Assert.Equal(Unit.G, byName?.BaseUnit);
        Assert.Equal(["Gouda"], byName?.Aliases);
        Assert.Equal(cheese, byAlias?.Id);
        Assert.Null(missing);
        Assert.Equal(cheese, Assert.Single(byIds).Id);
        Assert.Null(await catalog.GetIngredientAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Parallel_creates_of_the_same_name_keep_one_ingredient()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            _client.PostAsJsonAsync(IngredientsUrl, new { name = "Por", baseUnit = "piece" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    private async Task<Guid> CreateAsync(string name, string[] aliases, string category = "other", bool isStaple = false, string baseUnit = "piece")
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name, aliases, baseUnit, category, isStaple });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static IEnumerable<string?> Names(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToList();
}
