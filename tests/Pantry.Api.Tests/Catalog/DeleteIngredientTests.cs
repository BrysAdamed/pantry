using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Tests.Infrastructure;
using Pantry.Api.Tests.Infrastructure.TestModule;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class DeleteIngredientTests(PantryApiFactory factory) : IAsyncLifetime
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
    public async Task Unused_ingredient_is_deleted_and_its_names_are_free_again()
    {
        // Arrange
        var id = await factory.SeedIngredientAsync("Cebula", ["Cebulka"], Unit.Piece);

        // Act
        var response = await _client.DeleteAsync($"{IngredientsUrl}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{IngredientsUrl}/{id}")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ICatalogModule>().GetIngredientAsync(id));
        var recreated = await _client.PostAsJsonAsync(IngredientsUrl, new { name = "Cebulka", baseUnit = "piece" });
        Assert.Equal(HttpStatusCode.Created, recreated.StatusCode);
    }

    [Fact]
    public async Task Used_ingredient_is_kept_with_reference_counts_per_module()
    {
        // Arrange
        var id = await factory.SeedIngredientAsync("Cebula", [], Unit.Piece);
        await factory.AddTestReferencesAsync(id, 3);

        // Act
        var response = await _client.DeleteAsync($"{IngredientsUrl}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var references = Assert.Single(problem.GetProperty("references").EnumerateArray());
        Assert.Equal(TestIngredientReferenceHandler.ModuleName, references.GetProperty("module").GetString());
        Assert.Equal(3, references.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{id}")).StatusCode);
        Assert.Equal(3, await factory.CountTestReferencesAsync(id));
    }

    [Fact]
    public async Task References_list_the_modules_that_use_the_ingredient()
    {
        // Arrange
        var used = await factory.SeedIngredientAsync("Cebula", [], Unit.Piece);
        var unused = await factory.SeedIngredientAsync("Czosnek", [], Unit.Piece);
        await factory.AddTestReferencesAsync(used, 2);

        // Act
        var usedReferences = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{used}/references");
        var unusedReferences = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{unused}/references");

        // Assert
        var reference = Assert.Single(usedReferences.GetProperty("references").EnumerateArray());
        Assert.Equal(TestIngredientReferenceHandler.ModuleName, reference.GetProperty("module").GetString());
        Assert.Equal(2, reference.GetProperty("count").GetInt32());
        Assert.Empty(unusedReferences.GetProperty("references").EnumerateArray());
    }

    [Fact]
    public async Task References_of_an_unknown_ingredient_return_404()
    {
        var response = await _client.GetAsync($"{IngredientsUrl}/{Guid.NewGuid()}/references");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_ingredient_returns_404()
    {
        var response = await _client.DeleteAsync($"{IngredientsUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_requires_a_session()
    {
        var id = await factory.SeedIngredientAsync("Cebula", [], Unit.Piece);

        var response = await factory.CreateBrowserClient().DeleteAsync($"{IngredientsUrl}/{id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"{IngredientsUrl}/{id}")).StatusCode);
    }
}
