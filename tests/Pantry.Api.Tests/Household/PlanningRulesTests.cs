using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class PlanningRulesTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string PlanningRulesUrl = "/api/household/planning-rules";

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Planning_rules_require_a_session()
    {
        var response = await factory.CreateBrowserClient().GetAsync(PlanningRulesUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_new_household_has_no_planning_rules()
    {
        var client = await factory.CreateAdminAsync();

        Assert.Empty(await GetRulesAsync(client));
        Assert.Empty(await GetAgentRulesAsync());
    }

    [Fact]
    public async Task Added_rules_go_last_and_enabled()
    {
        var client = await factory.CreateAdminAsync();
        await AddAsync(client, "Weekday dinners under 30 minutes");

        var response = await client.PostAsJsonAsync(PlanningRulesUrl, new { text = "  Fish at least once a week  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var rule = await response.Content.ReadFromJsonAsync<Rule>();
        Assert.Equal(new Rule(rule!.Id, "Fish at least once a week", 1, true), rule);
        Assert.Equal(["Weekday dinners under 30 minutes", "Fish at least once a week"], (await GetRulesAsync(client)).Select(r => r.Text));
    }

    [Fact]
    public async Task Edit_changes_only_the_text()
    {
        var client = await factory.CreateAdminAsync();
        var rule = await AddAsync(client, "Fish once a week");

        var response = await client.PutAsJsonAsync($"{PlanningRulesUrl}/{rule.Id}/text", new { text = "Fish twice a week" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(rule with { Text = "Fish twice a week" }, await response.Content.ReadFromJsonAsync<Rule>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task Invalid_texts_are_rejected(int length)
    {
        var client = await factory.CreateAdminAsync();
        var rule = await AddAsync(client, "Fish once a week");
        var text = new string('a', length);

        var added = await client.PostAsJsonAsync(PlanningRulesUrl, new { text });
        var edited = await client.PutAsJsonAsync($"{PlanningRulesUrl}/{rule.Id}/text", new { text });
        var blank = await client.PostAsJsonAsync(PlanningRulesUrl, new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, edited.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(["Fish once a week"], (await GetRulesAsync(client)).Select(r => r.Text));
    }

    [Fact]
    public async Task A_rule_of_200_characters_is_accepted()
    {
        var client = await factory.CreateAdminAsync();

        var response = await client.PostAsJsonAsync(PlanningRulesUrl, new { text = new string('a', 200) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_rules_are_kept_but_left_out_of_the_agent_read()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        await AddAsync(client, "Weekday dinners under 30 minutes");
        var deepFrying = await AddAsync(client, "No deep frying");
        await AddAsync(client, "Fish at least once a week");

        // Act
        var response = await client.PutAsJsonAsync($"{PlanningRulesUrl}/{deepFrying.Id}/enabled", new { isEnabled = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<Rule>())!.IsEnabled);
        Assert.Equal([true, false, true], (await GetRulesAsync(client)).Select(r => r.IsEnabled));
        Assert.Equal(["Weekday dinners under 30 minutes", "Fish at least once a week"], (await GetAgentRulesAsync()).Select(r => r.Text));
    }

    [Fact]
    public async Task A_disabled_rule_can_be_enabled_again()
    {
        var client = await factory.CreateAdminAsync();
        var rule = await AddAsync(client, "No deep frying");
        await client.PutAsJsonAsync($"{PlanningRulesUrl}/{rule.Id}/enabled", new { isEnabled = false });

        await client.PutAsJsonAsync($"{PlanningRulesUrl}/{rule.Id}/enabled", new { isEnabled = true });

        Assert.Equal(["No deep frying"], (await GetAgentRulesAsync()).Select(r => r.Text));
    }

    [Fact]
    public async Task Reorder_sets_the_order_for_the_list_and_the_agent_read()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var a = await AddAsync(client, "A");
        var b = await AddAsync(client, "B");
        var c = await AddAsync(client, "C");
        await client.PutAsJsonAsync($"{PlanningRulesUrl}/{b.Id}/enabled", new { isEnabled = false });

        // Act
        var response = await client.PutAsJsonAsync($"{PlanningRulesUrl}/order", new { planningRuleIds = new[] { c.Id, b.Id, a.Id } });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reordered = await GetRulesAsync(client);
        Assert.Equal(["C", "B", "A"], reordered.Select(r => r.Text));
        Assert.Equal([0, 1, 2], reordered.Select(r => r.Position));
        Assert.Equal(["C", "A"], (await GetAgentRulesAsync()).Select(r => r.Text));
    }

    [Fact]
    public async Task Reorder_must_list_every_rule_once_and_changes_nothing_otherwise()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var ids = new List<Guid>();
        foreach (var text in new[] { "A", "B", "C" })
        {
            ids.Add((await AddAsync(client, text)).Id);
        }

        // Act
        var missing = await client.PutAsJsonAsync($"{PlanningRulesUrl}/order", new { planningRuleIds = ids[1..] });
        var duplicated = await client.PutAsJsonAsync($"{PlanningRulesUrl}/order", new { planningRuleIds = ids[1..].Append(ids[1]) });
        var unknown = await client.PutAsJsonAsync($"{PlanningRulesUrl}/order", new { planningRuleIds = ids[1..].Append(Guid.NewGuid()) });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(["A", "B", "C"], (await GetRulesAsync(client)).Select(r => r.Text));
    }

    [Fact]
    public async Task Remove_deletes_the_rule_and_closes_the_gap()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        await AddAsync(client, "A");
        var b = await AddAsync(client, "B");
        await AddAsync(client, "C");

        // Act
        var response = await client.DeleteAsync($"{PlanningRulesUrl}/{b.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var rules = await GetRulesAsync(client);
        Assert.Equal(["A", "C"], rules.Select(r => r.Text));
        Assert.Equal([0, 1], rules.Select(r => r.Position));
        Assert.Equal(2, (await AddAsync(client, "D")).Position);
    }

    [Fact]
    public async Task Concurrent_adds_get_distinct_positions()
    {
        var client = await factory.CreateAdminAsync();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(i => client.PostAsJsonAsync(PlanningRulesUrl, new { text = $"Rule {i}" })));

        Assert.Equal([0, 1, 2, 3, 4], (await GetRulesAsync(client)).Select(r => r.Position));
    }

    [Fact]
    public async Task Unknown_rule_returns_404()
    {
        var client = await factory.CreateAdminAsync();
        var id = Guid.NewGuid();

        var edited = await client.PutAsJsonAsync($"{PlanningRulesUrl}/{id}/text", new { text = "Anything" });
        var disabled = await client.PutAsJsonAsync($"{PlanningRulesUrl}/{id}/enabled", new { isEnabled = false });
        var removed = await client.DeleteAsync($"{PlanningRulesUrl}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, edited.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private static async Task<Rule> AddAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync(PlanningRulesUrl, new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Rule>())!;
    }

    private static async Task<IReadOnlyList<Rule>> GetRulesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<Rule>>(PlanningRulesUrl))!;

    private async Task<IReadOnlyList<HouseholdPlanningRule>> GetAgentRulesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IHouseholdModule>().GetEnabledPlanningRulesAsync();
    }

    private sealed record Rule(Guid Id, string Text, int Position, bool IsEnabled);
}
