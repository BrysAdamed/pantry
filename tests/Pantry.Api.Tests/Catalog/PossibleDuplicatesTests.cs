using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class PossibleDuplicatesTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string IngredientsUrl = "/api/catalog/ingredients";
    private const string PossibleDuplicatesUrl = "/api/catalog/possible-duplicates";
    private const string NotDuplicatesUrl = "/api/catalog/not-duplicates";

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllIngredientsAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Possible_duplicates_require_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var list = await anonymous.GetAsync(PossibleDuplicatesUrl);
        var mark = await anonymous.PostAsJsonAsync(NotDuplicatesUrl, new { firstIngredientId = Guid.NewGuid(), secondIngredientId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, mark.StatusCode);
    }

    [Fact]
    public async Task Similar_names_are_paired_ignoring_diacritics()
    {
        // Arrange
        var onion = await CreateAsync("Cebula");
        var yellowOnion = await CreateAsync("Cebula żółta");
        await CreateAsync("Mleko");

        // Act
        var pairs = await PairsAsync();

        // Assert
        var pair = Assert.Single(pairs);
        AssertPair(pair, onion, yellowOnion);
        var similarity = pair.GetProperty("similarity").GetDouble();
        Assert.InRange(similarity, 0.4, 0.99);
        Assert.Equal("Cebula żółta", Side(pair, yellowOnion).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Aliases_count_as_names()
    {
        var cheese = await CreateAsync("Ser żółty", "Gouda");
        var aged = await CreateAsync("Gouda młoda");

        var pair = Assert.Single(await PairsAsync());

        AssertPair(pair, cheese, aged);
    }

    [Fact]
    public async Task Pairs_are_sorted_most_similar_first()
    {
        // Arrange
        var onion = await CreateAsync("Cebula");
        var smallOnion = await CreateAsync("Cebulka");
        var tomato = await CreateAsync("Pomidor");
        var tomatoes = await CreateAsync("Pomidory");

        // Act
        var pairs = await PairsAsync();

        // Assert
        Assert.Equal(2, pairs.Count);
        AssertPair(pairs[0], tomato, tomatoes);
        AssertPair(pairs[1], onion, smallOnion);
        Assert.True(pairs[0].GetProperty("similarity").GetDouble() > pairs[1].GetProperty("similarity").GetDouble());
    }

    [Fact]
    public async Task Pairs_can_be_limited_to_one_ingredient()
    {
        await CreateAsync("Cebula");
        await CreateAsync("Cebulka");
        var tomato = await CreateAsync("Pomidor");
        await CreateAsync("Pomidory");

        var pairs = await PairsAsync($"?ingredientId={tomato}&limit=10");

        Assert.Contains(tomato, IdsOf(Assert.Single(pairs)));
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=501")]
    [InlineData("ingredientId=onion")]
    public async Task Invalid_queries_are_rejected(string query)
    {
        var response = await _client.GetAsync($"{PossibleDuplicatesUrl}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pair_marked_as_not_duplicates_is_no_longer_suggested()
    {
        // Arrange
        var onion = await CreateAsync("Cebula");
        var yellowOnion = await CreateAsync("Cebula żółta");
        var smallOnion = await CreateAsync("Cebulka");

        // Act
        var marked = await MarkAsync(yellowOnion, onion);
        var again = await MarkAsync(onion, yellowOnion);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(1, await CountNotDuplicatePairsAsync());
        var pairs = await PairsAsync();
        Assert.DoesNotContain(pairs, p => IdsOf(p).Order().SequenceEqual(new[] { onion, yellowOnion }.Order()));
        Assert.Contains(pairs, p => IdsOf(p).Order().SequenceEqual(new[] { onion, smallOnion }.Order()));
    }

    [Fact]
    public async Task Not_duplicates_need_two_existing_ingredients()
    {
        var onion = await CreateAsync("Cebula");

        var same = await MarkAsync(onion, onion);
        var missing = await _client.PostAsJsonAsync(NotDuplicatesUrl, new { firstIngredientId = onion });
        var unknown = await MarkAsync(onion, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Merging_away_an_ingredient_removes_its_not_duplicates_pairs()
    {
        // Arrange
        var onion = await CreateAsync("Cebula");
        var yellowOnion = await CreateAsync("Cebula żółta");
        var shallot = await CreateAsync("Szalotka");
        await MarkAsync(onion, yellowOnion);

        // Act
        var merged = await _client.PostAsJsonAsync($"{IngredientsUrl}/{shallot}/merge", new { sourceId = yellowOnion });

        // Assert
        Assert.Equal(HttpStatusCode.OK, merged.StatusCode);
        Assert.Equal(0, await CountNotDuplicatePairsAsync());
    }

    [Fact]
    public async Task Deleting_an_ingredient_removes_its_not_duplicates_pairs()
    {
        var onion = await CreateAsync("Cebula");
        var yellowOnion = await CreateAsync("Cebula żółta");
        await MarkAsync(onion, yellowOnion);

        var deleted = await _client.DeleteAsync($"{IngredientsUrl}/{onion}");

        Assert.True(deleted.IsSuccessStatusCode, deleted.StatusCode.ToString());
        Assert.Equal(0, await CountNotDuplicatePairsAsync());
    }

    [Fact]
    public async Task Threshold_is_configurable()
    {
        // Arrange
        await CreateAsync("Cebula");
        await CreateAsync("Cebulka");
        await CreateAsync("Pomidor");
        await CreateAsync("Pomidory");
        await using var strict = factory.WithWebHostBuilder(builder => builder.UseSetting($"{PossibleDuplicateSettings.Section}:Threshold", "0.6"));
        await using var scope = strict.Services.CreateAsyncScope();

        // Act
        var pairs = await scope.ServiceProvider.GetRequiredService<ICatalogModule>().GetPossibleDuplicatesAsync();

        // Assert
        var pair = Assert.Single(pairs);
        Assert.Equal(["Pomidor", "Pomidory"], new[] { pair.First.Name, pair.Second.Name }.Order());
    }

    [Fact]
    public async Task Contract_lists_and_marks_possible_duplicates()
    {
        // Arrange
        var onion = await CreateAsync("Cebula");
        var smallOnion = await CreateAsync("Cebulka");
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogModule>();

        // Act
        var before = await catalog.GetPossibleDuplicatesAsync(onion);
        var marked = await catalog.MarkNotDuplicatesAsync(smallOnion, onion);
        var unknown = await catalog.MarkNotDuplicatesAsync(onion, Guid.NewGuid());
        var after = await catalog.GetPossibleDuplicatesAsync();

        // Assert
        Assert.Equal(new[] { onion, smallOnion }.Order(), new[] { before.Single().First.Id, before.Single().Second.Id }.Order());
        Assert.True(marked);
        Assert.False(unknown);
        Assert.Empty(after);
    }

    [Fact]
    public void Pair_is_stored_in_postgres_uuid_order_whichever_way_it_is_marked()
    {
        var a = Guid.Parse("0a000000-0000-0000-0000-00000000000b");
        var b = Guid.Parse("0b000000-0000-0000-0000-00000000000a");

        var forward = NotDuplicatePair.Of(a, b);
        var backward = NotDuplicatePair.Of(b, a);

        Assert.Equal((a, b), (forward.FirstIngredientId, forward.SecondIngredientId));
        Assert.Equal((a, b), (backward.FirstIngredientId, backward.SecondIngredientId));
        Assert.Throws<ArgumentException>(() => NotDuplicatePair.Of(a, a));
    }

    private async Task<Guid> CreateAsync(string name, params string[] aliases)
    {
        var response = await _client.PostAsJsonAsync(IngredientsUrl, new { name, aliases, baseUnit = "piece" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> MarkAsync(Guid first, Guid second) =>
        _client.PostAsJsonAsync(NotDuplicatesUrl, new { firstIngredientId = first, secondIngredientId = second });

    private async Task<List<JsonElement>> PairsAsync(string query = "")
    {
        var response = await _client.GetFromJsonAsync<JsonElement>($"{PossibleDuplicatesUrl}{query}");
        return response.GetProperty("items").EnumerateArray().ToList();
    }

    private async Task<long> CountNotDuplicatePairsAsync()
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM catalog.not_duplicate_pairs", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static Guid[] IdsOf(JsonElement pair) =>
        [pair.GetProperty("first").GetProperty("id").GetGuid(), pair.GetProperty("second").GetProperty("id").GetGuid()];

    private static void AssertPair(JsonElement pair, Guid one, Guid other) =>
        Assert.Equal(new[] { one, other }.Order(), IdsOf(pair).Order());

    private static JsonElement Side(JsonElement pair, Guid id) =>
        pair.GetProperty("first").GetProperty("id").GetGuid() == id ? pair.GetProperty("first") : pair.GetProperty("second");
}
