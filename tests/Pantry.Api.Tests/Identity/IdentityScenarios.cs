using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Identity;

internal static class IdentityScenarios
{
    /// <summary>Runs the first run with <paramref name="authenticator"/> on a fresh client and returns that signed-in client.</summary>
    public static async Task<HttpClient> CreateAdminAsync(PantryApiFactory factory, SoftwareAuthenticator authenticator, string displayName = "Admin example")
    {
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/setup");
        var options = await client.PostAsJsonAsync("/api/setup/passkey-options", new { displayName });
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var credential = authenticator.Create(await options.Content.ReadFromJsonAsync<JsonElement>());
        var complete = await client.PostAsJsonAsync("/api/setup/complete", new { credential });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return client;
    }

    /// <summary>The SPA's sign-in: fetch a token, request options, answer them with the passkey, post the assertion.</summary>
    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, SoftwareAuthenticator authenticator)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/antiforgery")).StatusCode);
        var options = await client.PostAsync("/api/auth/passkey-options", null);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var credential = authenticator.Get(await options.Content.ReadFromJsonAsync<JsonElement>());
        return await client.PostAsJsonAsync("/api/auth/sign-in", new { credential });
    }

    public static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{name}=", StringComparison.Ordinal));

    public static void AssertCleared(HttpResponseMessage response, string name) =>
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookie(response, name), StringComparison.OrdinalIgnoreCase);
}
