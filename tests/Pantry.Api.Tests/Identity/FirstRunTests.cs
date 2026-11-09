using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Identity;

/// <summary>
/// First run against a real Postgres. Passkeys are created by <see cref="SoftwareAuthenticator"/>, so the API's
/// attestation verification runs for real (rp ID hash, challenge, origin, COSE key).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FirstRunTests(PantryApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Setup_reports_first_run_needed_on_a_fresh_database()
    {
        var client = factory.CreateBrowserClient();

        var status = await client.GetFromJsonAsync<JsonElement>("/api/setup");

        Assert.True(status.GetProperty("firstRunNeeded").GetBoolean());
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_without_a_session()
    {
        var client = factory.CreateBrowserClient();

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_run_creates_the_admin_with_a_passkey_and_signs_them_in()
    {
        // Arrange
        var client = factory.CreateBrowserClient();
        using var authenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);

        // Act
        var credential = await CreatePasskeyAsync(client, authenticator, "Admin example");
        var response = await CompleteAsync(client, credential);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var admin = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Admin example", admin.GetProperty("displayName").GetString());
        Assert.Equal("admin", admin.GetProperty("role").GetString());

        var sessionCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pantry.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Admin example", me.GetProperty("displayName").GetString());
        var status = await client.GetFromJsonAsync<JsonElement>("/api/setup");
        Assert.False(status.GetProperty("firstRunNeeded").GetBoolean());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = await db.Users.SingleAsync();
        Assert.Null(user.PasswordHash);
        Assert.Equal(authenticator.CredentialId, (await db.UserPasskeys.SingleAsync()).CredentialId);
    }

    [Fact]
    public async Task First_run_returns_409_once_an_admin_exists()
    {
        // Arrange
        var lateClient = factory.CreateBrowserClient();
        using var lateAuthenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var lateCredential = await CreatePasskeyAsync(lateClient, lateAuthenticator, "Second person");

        var firstClient = factory.CreateBrowserClient();
        using var firstAuthenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var firstResponse = await CompleteAsync(firstClient, await CreatePasskeyAsync(firstClient, firstAuthenticator, "First person"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act
        var lateComplete = await CompleteAsync(lateClient, lateCredential);
        var newOptions = await RequestOptionsAsync(factory.CreateBrowserClient(), "Third person");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, lateComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, newOptions.StatusCode);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task Concurrent_first_runs_create_exactly_one_admin()
    {
        // Arrange
        var clients = Enumerable.Range(0, 2).Select(_ => factory.CreateBrowserClient()).ToArray();
        var authenticators = clients.Select(_ => new SoftwareAuthenticator(PantryApiFactory.Origin)).ToArray();
        var credentials = new JsonObject[clients.Length];
        for (var i = 0; i < clients.Length; i++)
        {
            credentials[i] = await CreatePasskeyAsync(clients[i], authenticators[i], $"Person {i}");
        }

        // Act
        var responses = await Task.WhenAll(clients.Select((client, i) => CompleteAsync(client, credentials[i])));

        // Assert
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await CountUsersAsync());
        foreach (var authenticator in authenticators)
        {
            authenticator.Dispose();
        }
    }

    [Fact]
    public async Task Passkey_from_another_origin_is_rejected()
    {
        var client = factory.CreateBrowserClient();
        using var authenticator = new SoftwareAuthenticator("https://evil.example");

        var response = await CompleteAsync(client, await CreatePasskeyAsync(client, authenticator, "Someone"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountUsersAsync());
    }

    [Fact]
    public async Task Passkey_for_another_relying_party_is_rejected()
    {
        var client = factory.CreateBrowserClient();
        using var authenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var options = await (await RequestOptionsAsync(client, "Someone")).Content.ReadFromJsonAsync<JsonElement>();

        var response = await CompleteAsync(client, authenticator.Create(options, overrideRelyingPartyId: "evil.example"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Complete_without_passkey_options_returns_400()
    {
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/setup");
        using var authenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var credential = authenticator.Create(JsonDocument.Parse("""{"rp":{"id":"localhost"},"challenge":"AAAA"}""").RootElement);

        var response = await CompleteAsync(client, credential);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This display name is far too long to fit into fifty characters")]
    public async Task Passkey_options_reject_an_invalid_display_name(string displayName)
    {
        var response = await RequestOptionsAsync(factory.CreateBrowserClient(), displayName);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsafe_request_without_antiforgery_token_returns_400()
    {
        var client = factory.CreateClient(new() { BaseAddress = PantryApiFactory.BaseAddress });

        var response = await client.PostAsJsonAsync("/api/setup/passkey-options", new { displayName = "Someone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> RequestOptionsAsync(HttpClient client, string displayName)
    {
        await client.GetAsync("/api/setup");
        return await client.PostAsJsonAsync("/api/setup/passkey-options", new { displayName });
    }

    private static async Task<JsonObject> CreatePasskeyAsync(HttpClient client, SoftwareAuthenticator authenticator, string displayName)
    {
        var response = await RequestOptionsAsync(client, displayName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(displayName, options.GetProperty("user").GetProperty("displayName").GetString());
        return authenticator.Create(options);
    }

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, JsonObject credential) =>
        client.PostAsJsonAsync("/api/setup/complete", new { credential });

    private async Task<int> CountUsersAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Users.CountAsync();
    }
}
