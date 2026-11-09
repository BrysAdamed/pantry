using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Tests.Catalog;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class DislikesTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string DislikesUrl = "/api/household/dislikes";
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
    public async Task Dislikes_require_a_session()
    {
        var response = await factory.CreateBrowserClient().GetAsync(DislikesUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Added_dislikes_are_listed_never_first_with_ingredient_names()
    {
        // Arrange
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);
        var coriander = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var mushrooms = await factory.SeedIngredientAsync("Mushrooms", [], Unit.G);

        // Act
        var response = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId = walnuts, kind = "never", reason = "  allergy  " });
        await AddAsync(coriander, "avoid");
        await AddAsync(mushrooms, "never");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<DislikeDto>())!;
        Assert.Equal(new DislikeDto(added.Id, walnuts, "Walnuts", "never", "allergy"), added);
        Assert.Equal(
            [("Mushrooms", "never", null), ("Walnuts", "never", "allergy"), ("Coriander", "avoid", (string?)null)],
            (await GetAsync()).Select(d => (d.IngredientName, d.Kind, d.Reason)));
        var agentView = await GetAgentDislikesAsync();
        Assert.Equal([DislikeKind.Never, DislikeKind.Never, DislikeKind.Avoid], agentView.Select(d => d.Kind));
        Assert.Contains(new HouseholdDislike(walnuts, DislikeKind.Never, "allergy"), agentView);
    }

    [Fact]
    public async Task Unknown_or_missing_ingredient_is_rejected()
    {
        var unknown = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId = Guid.NewGuid(), kind = "never" });
        var missing = await _client.PostAsJsonAsync(DislikesUrl, new { kind = "never" });

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Empty(await GetAsync());
    }

    [Fact]
    public async Task A_second_dislike_of_the_same_ingredient_is_rejected()
    {
        // Arrange
        var coriander = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var first = await AddAsync(coriander, "avoid");

        // Act
        var response = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId = coriander, kind = "never" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.Id, problem.GetProperty("dislikeId").GetGuid());
        Assert.Equal([("avoid", (string?)null)], (await GetAsync()).Select(d => (d.Kind, d.Reason)));
    }

    [Fact]
    public async Task Parallel_adds_of_the_same_ingredient_keep_one_dislike()
    {
        var coriander = await factory.SeedIngredientAsync("Coriander", [], Unit.G);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _client.PostAsJsonAsync(DislikesUrl, new { ingredientId = coriander, kind = "avoid" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single(await GetAsync());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("sometimes", null)]
    [InlineData("avoid", "too spicy")]
    [InlineData("never", "101")]
    public async Task Invalid_kinds_and_reasons_are_rejected(string? kind, string? reason)
    {
        // Arrange
        var coriander = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);
        var existing = await AddAsync(walnuts, "never");
        reason = reason == "101" ? new string('a', 101) : reason;

        // Act
        var added = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId = coriander, kind, reason });
        var changed = await _client.PutAsJsonAsync($"{DislikesUrl}/{existing.Id}", new { kind, reason });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal([existing], await GetAsync());
    }

    [Fact]
    public async Task A_blank_reason_is_no_reason_and_100_characters_are_accepted()
    {
        var coriander = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);

        var blank = await AddAsync(coriander, "avoid", "   ");
        var longest = await AddAsync(walnuts, "never", new string('a', 100));

        Assert.Null(blank.Reason);
        Assert.Equal(100, longest.Reason!.Length);
    }

    [Fact]
    public async Task Change_sets_the_kind_and_reason_and_avoid_drops_the_reason()
    {
        // Arrange
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);
        var dislike = await AddAsync(walnuts, "avoid");

        // Act
        var toNever = await ChangeAsync(dislike.Id, "never", "allergy");
        var toAvoid = await ChangeAsync(dislike.Id, "avoid", null);

        // Assert
        Assert.Equal(dislike with { Kind = "never", Reason = "allergy" }, toNever);
        Assert.Equal(dislike, toAvoid);
        Assert.Equal([dislike], await GetAsync());
    }

    [Fact]
    public async Task Change_and_delete_of_an_unknown_dislike_return_404()
    {
        var changed = await _client.PutAsJsonAsync($"{DislikesUrl}/{Guid.NewGuid()}", new { kind = "avoid" });
        var deleted = await _client.DeleteAsync($"{DislikesUrl}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_dislike()
    {
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);
        var dislike = await AddAsync(walnuts, "never");

        var response = await _client.DeleteAsync($"{DislikesUrl}/{dislike.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetAsync());
    }

    [Fact]
    public async Task A_disliked_ingredient_cannot_be_deleted_from_the_catalog()
    {
        // Arrange
        var walnuts = await factory.SeedIngredientAsync("Walnuts", [], Unit.G);
        await AddAsync(walnuts, "never");

        // Act
        var response = await _client.DeleteAsync($"{IngredientsUrl}/{walnuts}");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var references = Assert.Single(problem.GetProperty("references").EnumerateArray());
        Assert.Equal("household", references.GetProperty("module").GetString());
        Assert.Equal(1, references.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Merge_moves_the_dislike_of_the_source_to_the_target()
    {
        // Arrange
        var target = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var source = await factory.SeedIngredientAsync("Cilantro", [], Unit.G);
        await AddAsync(source, "never", "allergy");

        // Act
        var preview = await _client.GetFromJsonAsync<JsonElement>($"{IngredientsUrl}/{target}/merge-preview?sourceId={source}");
        await MergeAsync(target, source);

        // Assert
        var household = Assert.Single(preview.GetProperty("references").EnumerateArray(), r => r.GetProperty("module").GetString() == "household");
        Assert.Equal((1, 0), (household.GetProperty("source").GetInt32(), household.GetProperty("target").GetInt32()));
        Assert.Equal([(target, "Coriander", "never", "allergy")], (await GetAsync()).Select(d => (d.IngredientId, d.IngredientName, d.Kind, d.Reason)));
    }

    [Theory]
    [InlineData("avoid", null, "never", "allergy", "never", "allergy")]
    [InlineData("never", "allergy", "avoid", null, "never", "allergy")]
    [InlineData("never", null, "never", "allergy", "never", "allergy")]
    [InlineData("never", "intolerance", "never", "allergy", "never", "intolerance")]
    [InlineData("avoid", null, "avoid", null, "avoid", null)]
    public async Task Merge_of_two_disliked_ingredients_keeps_one_dislike_with_the_stricter_kind(
        string targetKind, string? targetReason, string sourceKind, string? sourceReason, string kind, string? reason)
    {
        // Arrange
        var target = await factory.SeedIngredientAsync("Coriander", [], Unit.G);
        var source = await factory.SeedIngredientAsync("Cilantro", [], Unit.G);
        var targetDislike = await AddAsync(target, targetKind, targetReason);
        await AddAsync(source, sourceKind, sourceReason);

        // Act
        await MergeAsync(target, source);

        // Assert
        Assert.Equal([targetDislike with { Kind = kind, Reason = reason }], await GetAsync());
    }

    private async Task<DislikeDto> AddAsync(Guid ingredientId, string kind, string? reason = null)
    {
        var response = await _client.PostAsJsonAsync(DislikesUrl, new { ingredientId, kind, reason });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DislikeDto>())!;
    }

    private async Task<DislikeDto> ChangeAsync(Guid id, string kind, string? reason)
    {
        var response = await _client.PutAsJsonAsync($"{DislikesUrl}/{id}", new { kind, reason });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DislikeDto>())!;
    }

    private async Task MergeAsync(Guid target, Guid source)
    {
        var response = await _client.PostAsJsonAsync($"{IngredientsUrl}/{target}/merge", new { sourceId = source });
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<DislikeDto>> GetAsync() =>
        (await _client.GetFromJsonAsync<List<DislikeDto>>(DislikesUrl))!;

    private async Task<IReadOnlyList<HouseholdDislike>> GetAgentDislikesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IHouseholdModule>().GetDislikesAsync();
    }

    private sealed record DislikeDto(Guid Id, Guid IngredientId, string IngredientName, string Kind, string? Reason);
}
