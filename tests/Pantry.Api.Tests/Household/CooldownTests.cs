using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class CooldownTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string CooldownUrl = "/api/household/cooldown";

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cooldown_requires_a_session()
    {
        var anonymous = factory.CreateBrowserClient();

        var read = await anonymous.GetAsync(CooldownUrl);
        var write = await anonymous.PutAsJsonAsync(CooldownUrl, new { cooldownDays = 7 });

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
    }

    [Fact]
    public async Task Household_starts_with_three_weeks()
    {
        var client = await factory.CreateAdminAsync();

        var setting = await client.GetFromJsonAsync<JsonElement>(CooldownUrl);

        Assert.Equal(21, setting.GetProperty("cooldownDays").GetInt32());
        Assert.Equal(21, await GetForRecipesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(365)]
    public async Task Cooldown_can_be_changed(int days)
    {
        // Arrange
        var client = await factory.CreateAdminAsync();

        // Act
        var response = await client.PutAsJsonAsync(CooldownUrl, new { cooldownDays = days });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(days, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cooldownDays").GetInt32());
        Assert.Equal(days, (await client.GetFromJsonAsync<JsonElement>(CooldownUrl)).GetProperty("cooldownDays").GetInt32());
        Assert.Equal(days, await GetForRecipesAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    [InlineData(null)]
    public async Task Cooldown_outside_zero_to_a_year_is_rejected(int? days)
    {
        // Arrange
        var client = await factory.CreateAdminAsync();

        // Act
        var response = await client.PutAsJsonAsync(CooldownUrl, new { cooldownDays = days });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("cooldownDays", out _));
        Assert.Equal(21, await GetForRecipesAsync());
    }

    private async Task<int> GetForRecipesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IHouseholdModule>().GetCooldownDaysAsync();
    }
}
