using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;
using Pantry.Api.Tests.Infrastructure;
using Pantry.Api.Tests.Infrastructure.TestModule;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class MergeIngredientsTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string IngredientsUrl = "/api/catalog/ingredients";

    private HttpClient _client = null!;

    private TestIngredientReferenceFailure Failure => factory.Services.GetRequiredService<TestIngredientReferenceFailure>();

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        Failure.FailMoves = false;
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync()
    {
        Failure.FailMoves = false;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Merge_moves_names_and_references_deletes_the_source_and_confirms_the_target()
    {
        // Arrange
        var target = await SeedAsync("Cebula", ["Cebula biała"], Unit.Piece, needsReview: true, category: IngredientCategory.VegetablesAndFruit);
        var source = await SeedAsync("cebulka", ["Cebula żółta"], Unit.G, isStaple: true);
        await AddReferencesAsync(source, 2);
        await AddReferencesAsync(target, 1);

        // Act
        var response = await _client.PostAsJsonAsync($"{IngredientsUrl}/{target}/merge", new { sourceId = source });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var merged = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cebula", merged.GetProperty("name").GetString());
        Assert.Equal(["Cebula biała", "Cebula żółta", "cebulka"], merged.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("piece", merged.GetProperty("baseUnit").GetString());
        Assert.Equal("vegetablesAndFruit", merged.GetProperty("category").GetString());
        Assert.False(merged.GetProperty("isStaple").GetBoolean());
        Assert.False(merged.GetProperty("needsReview").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{IngredientsUrl}/{source}")).StatusCode);
        Assert.Equal(3, await CountReferencesAsync(target));
        Assert.Equal(0, await CountReferencesAsync(source));

        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();
        Assert.Equal(target, (await catalog.FindIngredientAsync("Cebulka"))?.Id);
    }

    [Fact]
    public async Task Preview_shows_the_plan_without_changing_anything()
    {
        // Arrange
        var target = await SeedAsync("Cebula", [], Unit.Piece);
        var source = await SeedAsync("Cebulka", ["Szalotka"], Unit.G);
        await AddReferencesAsync(source, 2);

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{target}/merge-preview?sourceId={source}");

        // Assert
        Assert.Equal(target, preview.GetProperty("target").GetProperty("id").GetGuid());
        Assert.Equal(source, preview.GetProperty("source").GetProperty("id").GetGuid());
        Assert.Equal(["Cebulka", "Szalotka"], preview.GetProperty("aliasesToAdd").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(2, preview.GetProperty("aliasCountAfter").GetInt32());
        Assert.False(preview.GetProperty("exceedsAliasLimit").GetBoolean());
        Assert.False(preview.GetProperty("sameBaseUnit").GetBoolean());
        var references = Assert.Single(preview.GetProperty("references").EnumerateArray());
        Assert.Equal(TestIngredientReferenceHandler.ModuleName, references.GetProperty("module").GetString());
        Assert.Equal(2, references.GetProperty("source").GetInt32());
        Assert.Equal(0, references.GetProperty("target").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{source}")).StatusCode);
        Assert.Equal(2, await CountReferencesAsync(source));
    }

    [Fact]
    public async Task Merging_into_itself_is_rejected()
    {
        var id = await SeedAsync("Cebula", [], Unit.Piece);

        var merge = await _client.PostAsJsonAsync($"{IngredientsUrl}/{id}/merge", new { sourceId = id });
        var preview = await _client.GetAsync($"{IngredientsUrl}/{id}/merge-preview?sourceId={id}");
        var missingSource = await _client.PostAsJsonAsync($"{IngredientsUrl}/{id}/merge", new { });

        Assert.Equal(HttpStatusCode.BadRequest, merge.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingSource.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Unknown_ingredients_return_404()
    {
        var id = await SeedAsync("Cebula", [], Unit.Piece);

        var unknownSource = await _client.PostAsJsonAsync($"{IngredientsUrl}/{id}/merge", new { sourceId = Guid.NewGuid() });
        var unknownTarget = await _client.PostAsJsonAsync($"{IngredientsUrl}/{Guid.NewGuid()}/merge", new { sourceId = id });
        var preview = await _client.GetAsync($"{IngredientsUrl}/{id}/merge-preview?sourceId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, unknownSource.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownTarget.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
    }

    [Fact]
    public async Task Failing_handler_rolls_the_whole_merge_back()
    {
        // Arrange
        var target = await SeedAsync("Cebula", [], Unit.Piece, needsReview: true);
        var source = await SeedAsync("Cebulka", [], Unit.Piece);
        await AddReferencesAsync(source, 2);
        Failure.FailMoves = true;

        // Act
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _client.PostAsJsonAsync($"{IngredientsUrl}/{target}/merge", new { sourceId = source }));

        // Assert
        Assert.Contains("test module", failure.Message);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{source}")).StatusCode);
        var unchanged = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{target}");
        Assert.Empty(unchanged.GetProperty("aliases").EnumerateArray());
        Assert.True(unchanged.GetProperty("needsReview").GetBoolean());
        Assert.Equal(2, await CountReferencesAsync(source));
        Assert.Equal(0, await CountReferencesAsync(target));
    }

    [Fact]
    public async Task Merge_that_would_exceed_the_alias_limit_is_refused()
    {
        // Arrange
        var target = await SeedAsync("Cebula", Enumerable.Range(1, Ingredient.MaxAliases - 1).Select(i => $"Cebula {i}").ToArray(), Unit.Piece);
        var source = await SeedAsync("Cebulka", ["Szalotka"], Unit.Piece);

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{target}/merge-preview?sourceId={source}");
        var response = await _client.PostAsJsonAsync($"{IngredientsUrl}/{target}/merge", new { sourceId = source });

        // Assert
        Assert.True(preview.GetProperty("exceedsAliasLimit").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{source}")).StatusCode);
    }

    [Fact]
    public async Task Opposite_parallel_merges_leave_one_ingredient()
    {
        var a = await SeedAsync("Cebula", [], Unit.Piece);
        var b = await SeedAsync("Cebulka", [], Unit.Piece);

        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync($"{IngredientsUrl}/{a}/merge", new { sourceId = b }),
            _client.PostAsJsonAsync($"{IngredientsUrl}/{b}/merge", new { sourceId = a }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NotFound);
        var list = await _client.GetFromJsonAsync<JsonElement>(IngredientsUrl);
        Assert.Equal(1, list.GetProperty("total").GetInt32());
    }

    private async Task<Guid> SeedAsync(
        string name,
        string[] aliases,
        Unit baseUnit,
        bool needsReview = false,
        bool isStaple = false,
        IngredientCategory category = IngredientCategory.Other)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var ingredient = Ingredient.Create(name, aliases, baseUnit, category, isStaple, needsReview);
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return ingredient.Id;
    }

    private async Task AddReferencesAsync(Guid ingredientId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        db.IngredientReferences.AddRange(Enumerable.Range(0, count).Select(_ => new TestIngredientReference { IngredientId = ingredientId }));
        await db.SaveChangesAsync();
    }

    private async Task<int> CountReferencesAsync(Guid ingredientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        return await db.IngredientReferences.CountAsync(r => r.IngredientId == ingredientId);
    }
}
