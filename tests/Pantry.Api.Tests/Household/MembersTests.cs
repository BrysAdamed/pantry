using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class MembersTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string MembersUrl = "/api/household/members";

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Members_require_a_session()
    {
        var response = await factory.CreateBrowserClient().GetAsync(MembersUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_run_creates_the_admins_member_with_the_default_factor()
    {
        var client = await factory.CreateAdminAsync("Admin example");

        var household = await client.GetFromJsonAsync<JsonElement>(MembersUrl);

        var admin = Assert.Single(household.GetProperty("members").EnumerateArray());
        Assert.Equal("Admin example", admin.GetProperty("name").GetString());
        Assert.Equal(1.0m, admin.GetProperty("portionFactor").GetDecimal());
        Assert.Equal("admin", admin.GetProperty("account").GetString());
        Assert.Equal(1.0m, household.GetProperty("householdServing").GetDecimal());
    }

    [Fact]
    public async Task Added_members_go_last_and_count_into_the_household_serving()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();

        // Act
        var created = await client.PostAsJsonAsync(MembersUrl, new { name = "  Toddler  ", portionFactor = 0.3m });
        await client.PostAsJsonAsync(MembersUrl, new { name = "Guest", portionFactor = 0.75m });
        var household = await client.GetFromJsonAsync<JsonElement>(MembersUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var toddler = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Toddler", toddler.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, toddler.GetProperty("account").ValueKind);
        Assert.Equal(
            ["Admin example", "Toddler", "Guest"],
            household.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(2.05m, household.GetProperty("householdServing").GetDecimal());
    }

    [Fact]
    public async Task Update_renames_a_member_and_changes_the_factor()
    {
        var client = await factory.CreateAdminAsync();
        var id = await AddAsync(client, "Toddler", 0.3m);

        var response = await client.PutAsJsonAsync($"{MembersUrl}/{id}", new { name = "Preschooler", portionFactor = 0.5m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var member = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Preschooler", member.GetProperty("name").GetString());
        Assert.Equal(0.5m, member.GetProperty("portionFactor").GetDecimal());
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(2.00)]
    public async Task Factor_bounds_are_accepted(decimal factor)
    {
        var client = await factory.CreateAdminAsync();

        var response = await client.PostAsJsonAsync(MembersUrl, new { name = "Someone", portionFactor = factor });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("Someone", 0)]
    [InlineData("Someone", 0.04)]
    [InlineData("Someone", 2.05)]
    [InlineData("Someone", 0.33)]
    [InlineData("Someone", -1)]
    [InlineData("", 1)]
    [InlineData("   ", 1)]
    [InlineData("This member name is far too long to fit into fifty characters", 1)]
    public async Task Invalid_members_are_rejected(string name, decimal factor)
    {
        var client = await factory.CreateAdminAsync();
        var id = await AddAsync(client, "Toddler", 0.3m);

        var added = await client.PostAsJsonAsync(MembersUrl, new { name, portionFactor = factor });
        var updated = await client.PutAsJsonAsync($"{MembersUrl}/{id}", new { name, portionFactor = factor });

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, updated.StatusCode);
    }

    [Fact]
    public async Task Member_without_an_account_can_be_deleted()
    {
        var client = await factory.CreateAdminAsync();
        var id = await AddAsync(client, "Guest", 1m);

        var response = await client.DeleteAsync($"{MembersUrl}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var household = await client.GetFromJsonAsync<JsonElement>(MembersUrl);
        Assert.Single(household.GetProperty("members").EnumerateArray());
    }

    [Fact]
    public async Task Member_linked_to_an_existing_user_cannot_be_deleted()
    {
        var client = await factory.CreateAdminAsync();
        var household = await client.GetFromJsonAsync<JsonElement>(MembersUrl);
        var adminId = household.GetProperty("members")[0].GetProperty("id").GetGuid();

        var response = await client.DeleteAsync($"{MembersUrl}/{adminId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_member_returns_404()
    {
        var client = await factory.CreateAdminAsync();
        var id = Guid.NewGuid();

        var updated = await client.PutAsJsonAsync($"{MembersUrl}/{id}", new { name = "Someone", portionFactor = 1m });
        var deleted = await client.DeleteAsync($"{MembersUrl}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task Reorder_sets_the_household_order()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var toddler = await AddAsync(client, "Toddler", 0.3m);
        var guest = await AddAsync(client, "Guest", 1m);
        var admin = (await client.GetFromJsonAsync<JsonElement>(MembersUrl)).GetProperty("members")[0].GetProperty("id").GetGuid();

        // Act
        var response = await client.PutAsJsonAsync($"{MembersUrl}/order", new { memberIds = new[] { guest, admin, toddler } });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var household = await client.GetFromJsonAsync<JsonElement>(MembersUrl);
        Assert.Equal(
            ["Guest", "Admin example", "Toddler"],
            household.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Reorder_must_list_every_member_once()
    {
        var client = await factory.CreateAdminAsync();
        var toddler = await AddAsync(client, "Toddler", 0.3m);

        var missing = await client.PutAsJsonAsync($"{MembersUrl}/order", new { memberIds = new[] { toddler } });
        var duplicated = await client.PutAsJsonAsync($"{MembersUrl}/order", new { memberIds = new[] { toddler, toddler } });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
    }

    [Fact]
    public async Task First_run_household_setup_rolls_back_with_the_identity_transaction()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var household = scope.ServiceProvider.GetRequiredService<IHouseholdModule>();

        // Act
        await using (var transaction = await identityDb.Database.BeginTransactionAsync())
        {
            await household.CompleteFirstRunAsync(new FirstRunAdmin(Guid.NewGuid(), "Rolled back", ContentLanguage.En), transaction.GetDbTransaction());
            await transaction.RollbackAsync();
        }

        // Assert
        var householdDb = scope.ServiceProvider.GetRequiredService<HouseholdDbContext>();
        Assert.False(await householdDb.Members.AnyAsync());
        Assert.False(await householdDb.MealSlots.AnyAsync());
    }

    private static async Task<Guid> AddAsync(HttpClient client, string name, decimal factor)
    {
        var response = await client.PostAsJsonAsync(MembersUrl, new { name, portionFactor = factor });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
