using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipesTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private HttpClient _client = null!;
    private Guid _flour;
    private Guid _milk;
    private Guid _salt;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
        _flour = await factory.SeedIngredientAsync("Mąka pszenna", [], Unit.G);
        _milk = await factory.SeedIngredientAsync("Mleko", [], Unit.Ml);
        _salt = await factory.SeedIngredientAsync("Sól", [], Unit.G);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Recipes_require_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var list = await anonymous.GetAsync(RecipesUrl);
        var tags = await anonymous.GetAsync($"{RecipesUrl}/tags");
        var created = await anonymous.PostAsJsonAsync(RecipesUrl, new { name = "Naleśniki", servings = 2 });

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tags.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, created.StatusCode);
    }

    [Fact]
    public async Task Created_recipe_keeps_entered_quantities_and_quantities_per_serving()
    {
        // Act
        var response = await _client.PostAsJsonAsync(RecipesUrl, Pancakes());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"/api/recipes/{created.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{created.GetProperty("id").GetGuid()}");
        Assert.Equal("Naleśniki", fetched.GetProperty("name").GetString());
        Assert.Equal(4, fetched.GetProperty("servings").GetInt32());
        Assert.Equal(25, fetched.GetProperty("prepTimeMinutes").GetInt32());
        Assert.Equal("https://example.com/nalesniki", fetched.GetProperty("sourceUrl").GetString());
        Assert.Equal(["na słodko", "śniadanie"], fetched.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(["Wymieszaj.", "Smaż."], fetched.GetProperty("steps").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(520, fetched.GetProperty("nutrition").GetProperty("kcal").GetDecimal());
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("nutrition").GetProperty("fat").ValueKind);
        Assert.True(fetched.GetProperty("makeAgain").GetBoolean());
        Assert.False(fetched.GetProperty("isArchived").GetBoolean());

        var salt = Assert.Single(fetched.GetProperty("lines").EnumerateArray());
        Assert.Equal(_salt, salt.GetProperty("ingredientId").GetGuid());
        Assert.Equal("Sól", salt.GetProperty("ingredientName").GetString());
        Assert.Equal(JsonValueKind.Null, salt.GetProperty("quantity").ValueKind);
        Assert.Equal(JsonValueKind.Null, salt.GetProperty("unit").ValueKind);

        var batter = Assert.Single(fetched.GetProperty("sections").EnumerateArray());
        Assert.Equal("Ciasto", batter.GetProperty("name").GetString());
        var lines = batter.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(_flour, lines[0].GetProperty("ingredientId").GetGuid());
        Assert.Equal(250, lines[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(62.5m, lines[0].GetProperty("quantityPerServing").GetDecimal());
        Assert.Equal("g", lines[0].GetProperty("unit").GetString());
        Assert.Equal("przesiana", lines[0].GetProperty("note").GetString());
        Assert.Equal(0.5m, lines[1].GetProperty("quantity").GetDecimal());
        Assert.Equal(0.125m, lines[1].GetProperty("quantityPerServing").GetDecimal());
        Assert.Equal("l", lines[1].GetProperty("unit").GetString());
    }

    [Fact]
    public async Task Line_with_a_new_ingredient_name_creates_an_ingredient_that_needs_review()
    {
        // Act
        var response = await _client.PostAsJsonAsync(RecipesUrl, new
        {
            name = "Jajecznica",
            servings = 2,
            lines = new object[]
            {
                new { ingredientName = "  Jajka ", quantity = 4, unit = "piece" },
                new { ingredientName = "mleko", quantity = 2, unit = "tbsp" },
            },
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lines = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal("Jajka", lines[0].GetProperty("ingredientName").GetString());
        Assert.True(lines[0].GetProperty("ingredientNeedsReview").GetBoolean());
        Assert.Equal(_milk, lines[1].GetProperty("ingredientId").GetGuid());
        Assert.False(lines[1].GetProperty("ingredientNeedsReview").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var eggs = await scope.ServiceProvider.GetRequiredService<ICatalogModule>().FindIngredientAsync("jajka");
        Assert.Equal(Unit.Piece, eggs?.BaseUnit);
        Assert.True(eggs?.NeedsReview);
    }

    [Fact]
    public async Task Update_replaces_the_recipe_and_recomputes_quantities_per_serving()
    {
        // Arrange
        var id = await CreateAsync(Pancakes());

        // Act
        var response = await _client.PutAsJsonAsync($"{RecipesUrl}/{id}", new
        {
            name = "Naleśniki z twarogiem",
            servings = 2,
            tags = new[] { "Obiad" },
            steps = new[] { "Usmaż." },
            makeAgain = false,
            cooldownDays = 14,
            lines = new object[] { new { ingredientId = _flour, quantity = 250, unit = "g" } },
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{id}");
        Assert.Equal("Naleśniki z twarogiem", fetched.GetProperty("name").GetString());
        Assert.Equal(["obiad"], fetched.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(["Usmaż."], fetched.GetProperty("steps").EnumerateArray().Select(t => t.GetString()));
        Assert.False(fetched.GetProperty("makeAgain").GetBoolean());
        Assert.Equal(14, fetched.GetProperty("cooldownDays").GetInt32());
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("prepTimeMinutes").ValueKind);
        Assert.Equal(JsonValueKind.Null, fetched.GetProperty("nutrition").GetProperty("kcal").ValueKind);
        Assert.Empty(fetched.GetProperty("sections").EnumerateArray());
        var line = Assert.Single(fetched.GetProperty("lines").EnumerateArray());
        Assert.Equal(125, line.GetProperty("quantityPerServing").GetDecimal());
    }

    [Fact]
    public async Task Update_without_make_again_keeps_it()
    {
        // Arrange
        var id = await CreateAsync(new { name = "Leczo", servings = 2, makeAgain = false });

        // Act
        var response = await _client.PutAsJsonAsync($"{RecipesUrl}/{id}", new { name = "Leczo z cukinią", servings = 2 });

        // Assert
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("makeAgain").GetBoolean());
    }

    [Fact]
    public async Task Unknown_recipe_returns_404()
    {
        var get = await _client.GetAsync($"{RecipesUrl}/{Guid.NewGuid()}");
        var put = await _client.PutAsJsonAsync($"{RecipesUrl}/{Guid.NewGuid()}", new { name = "Leczo", servings = 2 });
        var archive = await _client.PostAsync($"{RecipesUrl}/{Guid.NewGuid()}/archive", null);
        var restore = await _client.PostAsync($"{RecipesUrl}/{Guid.NewGuid()}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restore.StatusCode);
    }

    [Theory]
    [InlineData("""{ "servings": 2 }""", "Name")]
    [InlineData("""{ "name": "   ", "servings": 2 }""", "Name")]
    [InlineData("""{ "name": "Leczo" }""", "Servings")]
    [InlineData("""{ "name": "Leczo", "servings": 0 }""", "Servings")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "prepTimeMinutes": 0 }""", "PrepTimeMinutes")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "sourceUrl": "ftp://example.com" }""", "SourceUrl")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "cooldownDays": -1 }""", "CooldownDays")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "tags": [" "] }""", "Tags")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "steps": ["Pokrój.", ""] }""", "Steps[1]")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "nutrition": { "kcal": -1 } }""", "Nutrition")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "lines": [{ "ingredientName": "Cukinia", "quantity": 0, "unit": "g" }] }""", "Lines[0].Quantity")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "lines": [{ "ingredientName": "Cukinia", "quantity": 1.0005, "unit": "g" }] }""", "Lines[0].Quantity")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "lines": [{ "ingredientName": "Cukinia", "unit": "g" }] }""", "Lines[0].Unit")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "lines": [{ "ingredientName": "Cukinia", "quantity": 1 }] }""", "Lines[0].Unit")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "lines": [{ "quantity": 1, "unit": "g" }] }""", "Lines[0].Ingredient")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "sections": [{ "name": "Sos", "lines": [] }] }""", "Sections[0].Lines")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "sections": [{ "name": "", "lines": [{ "ingredientName": "Cukinia" }] }] }""", "Sections[0].Name")]
    [InlineData("""{ "name": "Leczo", "servings": 2, "sections": [{ "name": "Sos", "lines": [{ "ingredientName": "Cukinia" }] }, { "name": "SOS", "lines": [{ "ingredientName": "Papryka" }] }] }""", "Sections[1].Name")]
    public async Task Invalid_recipes_are_rejected_without_creating_ingredients(string json, string field)
    {
        // Act
        var response = await _client.PostAsync(RecipesUrl, new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
        Assert.Equal(0, (await ListAsync()).GetProperty("total").GetInt32());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ICatalogModule>().FindIngredientAsync("Cukinia"));
    }

    [Fact]
    public async Task Unknown_ingredient_id_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new
        {
            name = "Leczo",
            servings = 2,
            lines = new object[] { new { ingredientName = "Cukinia" }, new { ingredientId = Guid.NewGuid() } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("Lines[1].IngredientId", out _));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ICatalogModule>().FindIngredientAsync("Cukinia"));
    }

    [Fact]
    public async Task Archived_recipes_leave_the_library_and_come_back_when_restored()
    {
        // Arrange
        var soup = await CreateAsync(new { name = "Pomidorowa", servings = 4 });
        var pancakes = await CreateAsync(new { name = "naleśniki", servings = 2 });

        // Act
        var archived = await _client.PostAsync($"{RecipesUrl}/{soup}/archive", null);
        var again = await _client.PostAsync($"{RecipesUrl}/{soup}/archive", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal([pancakes], IdsOf(await ListAsync()));
        Assert.Equal([soup], IdsOf(await ListAsync("?archived=true")));
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{soup}");
        Assert.True(fetched.GetProperty("isArchived").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, fetched.GetProperty("archivedAt").ValueKind);

        var restored = await _client.PostAsync($"{RecipesUrl}/{soup}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal([pancakes, soup], IdsOf(await ListAsync()));
        Assert.Empty(IdsOf(await ListAsync("?archived=true")));
    }

    [Fact]
    public async Task Library_is_paged_in_name_order()
    {
        // Arrange
        var c = await CreateAsync(new { name = "Curry", servings = 2 });
        var a = await CreateAsync(new { name = "awokado", servings = 2 });
        var b = await CreateAsync(new { name = "Bigos", servings = 2 });

        // Act
        var first = await ListAsync("?pageSize=2");
        var second = await ListAsync("?pageSize=2&page=2");
        var invalid = await _client.GetAsync($"{RecipesUrl}?pageSize=0");

        // Assert
        Assert.Equal([a, b], IdsOf(first));
        Assert.Equal([c], IdsOf(second));
        Assert.Equal(3, first.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Tags_are_suggested_most_used_first_without_archived_recipes()
    {
        // Arrange
        await CreateAsync(new { name = "Pomidorowa", servings = 4, tags = new[] { "obiad", "szybkie" } });
        await CreateAsync(new { name = "Leczo", servings = 2, tags = new[] { "Obiad", "szybkie" } });
        await CreateAsync(new { name = "Owsianka", servings = 1, tags = new[] { "śniadanie", "szybkie" } });
        var archived = await CreateAsync(new { name = "Bigos", servings = 8, tags = new[] { "zima", "obiad" } });
        await _client.PostAsync($"{RecipesUrl}/{archived}/archive", null);

        // Act
        var all = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/tags");
        var searched = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/tags?search=%20OB");

        // Assert
        Assert.Equal(
            ["szybkie:3", "obiad:2", "śniadanie:1"],
            all.EnumerateArray().Select(t => $"{t.GetProperty("tag").GetString()}:{t.GetProperty("recipes").GetInt32()}"));
        Assert.Equal(["obiad"], searched.EnumerateArray().Select(t => t.GetProperty("tag").GetString()));
    }

    [Fact]
    public async Task Contract_reads_summaries_and_ingredient_lines_per_serving_including_archived_recipes()
    {
        // Arrange
        var pancakes = await CreateAsync(Pancakes());
        var soup = await CreateAsync(new { name = "Pomidorowa", servings = 3, lines = new object[] { new { ingredientId = _salt, quantity = 1, unit = "tsp" } } });
        await _client.PostAsync($"{RecipesUrl}/{soup}/archive", null);
        await using var scope = factory.Services.CreateAsyncScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        var summaries = await recipes.GetRecipesAsync([soup, pancakes, Guid.NewGuid()]);
        var single = await recipes.GetRecipeAsync(soup);
        var missing = await recipes.GetRecipeAsync(Guid.NewGuid());
        var lines = await recipes.GetIngredientsPerServingAsync([pancakes, soup]);

        // Assert
        Assert.Equal([pancakes, soup], summaries.Select(s => s.Id));
        Assert.True(single?.IsArchived);
        Assert.Equal(3, single?.Servings);
        Assert.Null(missing);
        Assert.Equal(["na słodko", "śniadanie"], summaries[0].Tags);
        Assert.Equal(520, summaries[0].Nutrition.Kcal);

        var pancakeLines = lines.Where(l => l.RecipeId == pancakes).ToList();
        Assert.Equal([_salt, _flour, _milk], pancakeLines.Select(l => l.IngredientId));
        Assert.Null(pancakeLines[0].Quantity);
        Assert.Equal(62.5m, pancakeLines[1].Quantity);
        Assert.Equal("Ciasto", pancakeLines[1].Section);
        Assert.Equal(0.125m, pancakeLines[2].Quantity);
        Assert.Equal(Unit.L, pancakeLines[2].Unit);
        var soupLine = Assert.Single(lines, l => l.RecipeId == soup);
        Assert.Equal(0.333333m, soupLine.Quantity);
        Assert.Equal(new QuantityLine(_salt, 0.999999m, Unit.Tsp), soupLine.ToQuantityLine(3));
    }

    private object Pancakes() => new
    {
        name = "  Naleśniki ",
        servings = 4,
        prepTimeMinutes = 25,
        sourceUrl = "https://example.com/nalesniki",
        tags = new[] { "Śniadanie", " na  słodko", "śniadanie" },
        steps = new[] { " Wymieszaj. ", "Smaż." },
        nutrition = new { kcal = 520, protein = 24, carbs = 68 },
        lines = new object[] { new { ingredientId = _salt } },
        sections = new object[]
        {
            new
            {
                name = " Ciasto ",
                lines = new object[]
                {
                    new { ingredientId = _flour, quantity = 250, unit = "g", note = " przesiana " },
                    new { ingredientId = _milk, quantity = 0.5, unit = "l" },
                },
            },
        },
    };

    private async Task<Guid> CreateAsync(object recipe)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, recipe);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ListAsync(string query = "") => await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}{query}");

    private static List<Guid> IdsOf(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
}
