using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Household;

[Collection(ApiCollection.Name)]
public sealed class ContentLanguageTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string ContentLanguageUrl = "/api/household/content-language";

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Content_language_requires_a_session()
    {
        var response = await factory.CreateBrowserClient().GetAsync(ContentLanguageUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("en", "en")]
    [InlineData("pl", "pl")]
    public async Task First_run_sets_the_content_language_to_the_admins_ui_language(string? uiLanguage, string expected)
    {
        var client = await factory.CreateAdminAsync(uiLanguage: uiLanguage);

        Assert.Equal(new Language(expected), await client.GetFromJsonAsync<Language>(ContentLanguageUrl));
        Assert.Equal(expected, (await GetForAgentsAsync()).Code);
    }

    [Fact]
    public async Task Changing_the_content_language_keeps_the_meal_slot_names()
    {
        var client = await factory.CreateAdminAsync(uiLanguage: "pl");

        var response = await client.PutAsJsonAsync(ContentLanguageUrl, new { contentLanguage = "de" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Language("de"), await response.Content.ReadFromJsonAsync<Language>());
        Assert.Equal(ContentLanguage.De, await GetForAgentsAsync());
        var slots = await client.GetFromJsonAsync<List<Slot>>("/api/household/meal-slots");
        Assert.Equal("Śniadanie", slots![0].Name);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Unsupported_languages_are_rejected(string? code)
    {
        var client = await factory.CreateAdminAsync(uiLanguage: "pl");

        var response = await client.PutAsJsonAsync(ContentLanguageUrl, new { contentLanguage = code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ContentLanguage.Pl, await GetForAgentsAsync());
    }

    [Fact]
    public async Task First_run_household_setup_keeps_a_content_language_that_is_already_set()
    {
        // Arrange
        var client = await factory.CreateAdminAsync();
        await client.PutAsJsonAsync(ContentLanguageUrl, new { contentLanguage = "fr" });
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
        Assert.Equal(ContentLanguage.Fr, await GetForAgentsAsync());
    }

    private async Task<ContentLanguage> GetForAgentsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IHouseholdModule>().GetContentLanguageAsync();
    }

    private sealed record Language(string ContentLanguage);

    private sealed record Slot(Guid Id, string Name);
}
