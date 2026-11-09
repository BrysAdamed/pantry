using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Infrastructure.DataProtection;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

[Collection(ApiCollection.Name)]
public sealed class SignInTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string SessionCookie = "pantry.session";
    private const string CeremonyCookie = "pantry.passkey";

    private readonly SoftwareAuthenticator _authenticator = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _authenticator.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sign_in_with_a_registered_passkey_starts_a_persistent_session()
    {
        // Arrange
        await CreateAdminAsync(factory, _authenticator);
        var client = factory.CreateBrowserClient();

        // Act
        var response = await SignInAsync(client, _authenticator);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin example", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("displayName").GetString());
        var session = SetCookie(response, SessionCookie);
        Assert.Contains("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
        AssertCleared(response, CeremonyCookie);
        SetCookie(response, ApiAntiforgery.RequestTokenCookieName);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Admin example", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Sign_in_with_an_unknown_passkey_returns_401_and_no_session()
    {
        // Arrange
        await CreateAdminAsync(factory, _authenticator);
        using var stranger = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var client = factory.CreateBrowserClient();

        // Act
        var response = await SignInAsync(client, stranger);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            c => c.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Sign_in_without_passkey_options_returns_401()
    {
        await CreateAdminAsync(factory, _authenticator);
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/antiforgery");
        var credential = _authenticator.Get(JsonDocument.Parse("""{"rpId":"localhost","challenge":"AAAA"}""").RootElement);

        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { credential });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_clears_the_session_and_hands_out_an_anonymous_token()
    {
        // Arrange
        var client = await CreateAdminAsync(factory, _authenticator);

        // Act
        var response = await client.PostAsync("/api/auth/sign-out", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertCleared(response, SessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/passkey-options", null)).StatusCode);
    }

    [Fact]
    public async Task Sign_in_works_after_the_session_ended_with_a_stale_token()
    {
        // Arrange
        await CreateAdminAsync(factory, _authenticator);
        var cookies = new CookieContainerHandler();
        var client = factory.CreateBrowserClient(cookies);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, _authenticator)).StatusCode);
        foreach (System.Net.Cookie cookie in cookies.Container.GetCookies(PantryApiFactory.BaseAddress))
        {
            if (cookie.Name == SessionCookie)
            {
                cookie.Expired = true;
            }
        }

        // Act
        var staleOptions = await client.PostAsync("/api/auth/passkey-options", null);
        var signIn = await SignInAsync(client, _authenticator);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, staleOptions.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Session_survives_an_app_restart()
    {
        // Arrange
        var cookies = new CookieContainerHandler();
        await CreateAdminAsync(factory, _authenticator);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(factory.CreateBrowserClient(cookies), _authenticator)).StatusCode);

        // Act
        await using var restarted = factory.WithWebHostBuilder(_ => { });
        var response = await PantryApiFactory.CreateBrowserClient(restarted, new CookieContainerHandler(cookies.Container)).GetAsync("/api/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>().DataProtectionKeys.AnyAsync());
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public async Task Safe_methods_need_no_antiforgery_token(string method)
    {
        var client = factory.CreateClient(new() { BaseAddress = PantryApiFactory.BaseAddress });

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/setup"));

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Api_responses_are_not_stored_by_the_browser()
    {
        var response = await factory.CreateBrowserClient().GetAsync("/api/setup");

        Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
