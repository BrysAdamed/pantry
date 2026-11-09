using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class MealSlotsTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string MealSlotsUrl = "/api/household/meal-slots";

    private static readonly string[] EnglishDefaults = ["Breakfast", "Second breakfast", "Lunch", "Snack", "Dinner"];

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Meal_slots_require_a_session()
    {
        var response = await factory.CreateBrowserClient().GetAsync(MealSlotsUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_run_creates_the_default_slots_in_english_without_a_language()
    {
        var client = await factory.CreateAdminAsync();

        var slots = await GetSlotsAsync(client);

        Assert.Equal(EnglishDefaults, slots.Select(s => s.Name));
        Assert.Equal([0, 1, 2, 3, 4], slots.Select(s => s.Position));
        Assert.All(slots, s => Assert.True(s.IsEnabled));
    }

    [Fact]
    public async Task First_run_names_the_default_slots_in_the_admins_ui_language()
    {
        var client = await factory.CreateAdminAsync(uiLanguage: "pl");

        var slots = await GetSlotsAsync(client);

        Assert.Equal(["Śniadanie", "II śniadanie", "Obiad", "Podwieczorek", "Kolacja"], slots.Select(s => s.Name));
    }

    [Fact]
    public async Task Added_slots_go_last_and_enabled()
    {
        var client = await factory.CreateAdminAsync();

        var response = await client.PostAsJsonAsync(MealSlotsUrl, new { name = "  Late supper  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var slot = await response.Content.ReadFromJsonAsync<Slot>();
        Assert.Equal(new Slot(slot!.Id, "Late supper", 5, true), slot);
        Assert.Equal("Late supper", (await GetSlotsAsync(client))[^1].Name);
    }

    [Fact]
    public async Task Rename_changes_only_the_name()
    {
        var client = await factory.CreateAdminAsync();
        var lunch = (await GetSlotsAsync(client))[2];

        var response = await client.PutAsJsonAsync($"{MealSlotsUrl}/{lunch.Id}/name", new { name = "Main meal" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(lunch with { Name = "Main meal" }, await response.Content.ReadFromJsonAsync<Slot>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This meal slot name is far too long to fit into fifty characters")]
    public async Task Invalid_names_are_rejected(string name)
    {
        var client = await factory.CreateAdminAsync();
        var breakfast = (await GetSlotsAsync(client))[0];

        var added = await client.PostAsJsonAsync(MealSlotsUrl, new { name });
        var renamed = await client.PutAsJsonAsync($"{MealSlotsUrl}/{breakfast.Id}/name", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
    }

    [Fact]
    public async Task Disabled_slots_stay_in_the_list_and_leave_the_planner_rows()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var slots = await GetSlotsAsync(client);

        // Act
        var response = await client.PutAsJsonAsync($"{MealSlotsUrl}/{slots[1].Id}/enabled", new { isEnabled = false });
        await client.PutAsJsonAsync($"{MealSlotsUrl}/{slots[3].Id}/enabled", new { isEnabled = false });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<Slot>())!.IsEnabled);
        Assert.Equal([true, false, true, false, true], (await GetSlotsAsync(client)).Select(s => s.IsEnabled));
        Assert.Equal(["Breakfast", "Lunch", "Dinner"], (await GetEnabledSlotsAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task The_last_enabled_slot_cannot_be_disabled()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var slots = await GetSlotsAsync(client);
        foreach (var slot in slots.Skip(1))
        {
            await client.PutAsJsonAsync($"{MealSlotsUrl}/{slot.Id}/enabled", new { isEnabled = false });
        }

        // Act
        var response = await client.PutAsJsonAsync($"{MealSlotsUrl}/{slots[0].Id}/enabled", new { isEnabled = false });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(["Breakfast"], (await GetEnabledSlotsAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task Concurrent_disables_keep_one_slot_enabled()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var slots = await GetSlotsAsync(client);
        foreach (var slot in slots.Skip(2))
        {
            await client.PutAsJsonAsync($"{MealSlotsUrl}/{slot.Id}/enabled", new { isEnabled = false });
        }

        // Act
        var responses = await Task.WhenAll(slots.Take(2).Select(slot =>
            client.PutAsJsonAsync($"{MealSlotsUrl}/{slot.Id}/enabled", new { isEnabled = false })));

        // Assert
        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.Conflict],
            responses.Select(r => r.StatusCode).Order());
        Assert.Single(await GetEnabledSlotsAsync());
    }

    [Fact]
    public async Task Reorder_sets_the_planner_order_without_gaps()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var slots = await GetSlotsAsync(client);
        await client.PutAsJsonAsync($"{MealSlotsUrl}/{slots[0].Id}/enabled", new { isEnabled = false });
        var order = new[] { slots[4], slots[0], slots[1], slots[2], slots[3] }.Select(s => s.Id).ToArray();

        // Act
        var response = await client.PutAsJsonAsync($"{MealSlotsUrl}/order", new { mealSlotIds = order });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var reordered = await GetSlotsAsync(client);
        Assert.Equal(order, reordered.Select(s => s.Id));
        Assert.Equal([0, 1, 2, 3, 4], reordered.Select(s => s.Position));
        Assert.Equal(["Dinner", "Second breakfast", "Lunch", "Snack"], (await GetEnabledSlotsAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task Reorder_must_list_every_slot_once_and_changes_nothing_otherwise()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        var ids = (await GetSlotsAsync(client)).Select(s => s.Id).ToArray();

        // Act
        var missing = await client.PutAsJsonAsync($"{MealSlotsUrl}/order", new { mealSlotIds = ids[1..] });
        var duplicated = await client.PutAsJsonAsync($"{MealSlotsUrl}/order", new { mealSlotIds = ids[1..].Append(ids[1]) });
        var unknown = await client.PutAsJsonAsync($"{MealSlotsUrl}/order", new { mealSlotIds = ids[1..].Append(Guid.NewGuid()) });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicated.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(EnglishDefaults, (await GetSlotsAsync(client)).Select(s => s.Name));
    }

    [Fact]
    public async Task Unknown_slot_returns_404()
    {
        var client = await factory.CreateAdminAsync();
        var id = Guid.NewGuid();

        var renamed = await client.PutAsJsonAsync($"{MealSlotsUrl}/{id}/name", new { name = "Brunch" });
        var disabled = await client.PutAsJsonAsync($"{MealSlotsUrl}/{id}/enabled", new { isEnabled = false });

        Assert.Equal(HttpStatusCode.NotFound, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
    }

    [Fact]
    public async Task First_run_household_setup_keeps_what_exists_when_called_again()
    {
        // Arrange
        await factory.CreateAdminAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var household = scope.ServiceProvider.GetRequiredService<IHouseholdModule>();
        var admin = (await household.GetMembersAsync()).Single();

        // Act
        await using (var transaction = await identityDb.Database.BeginTransactionAsync())
        {
            await household.CompleteFirstRunAsync(new FirstRunAdmin(admin.UserId!.Value, admin.Name, ContentLanguage.Pl), transaction.GetDbTransaction());
            await transaction.CommitAsync();
        }

        // Assert
        Assert.Single(await household.GetMembersAsync());
        var householdDb = scope.ServiceProvider.GetRequiredService<HouseholdDbContext>();
        Assert.Equal(EnglishDefaults, await householdDb.MealSlots.OrderBy(s => s.Position).Select(s => s.Name).ToListAsync());
    }

    private static async Task<IReadOnlyList<Slot>> GetSlotsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<Slot>>(MealSlotsUrl))!;

    private async Task<IReadOnlyList<HouseholdMealSlot>> GetEnabledSlotsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IHouseholdModule>().GetEnabledMealSlotsAsync();
    }

    private sealed record Slot(Guid Id, string Name, int Position, bool IsEnabled);
}
