using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pantry.Api.Tests.Infrastructure;

public static class SignedInClient
{
    /// <summary>
    /// Runs the first run on a fresh browser client and returns it signed in as the admin. Pass an
    /// <paramref name="authenticator"/> to sign in with the admin's passkey later; otherwise a throwaway one is used.
    /// </summary>
    public static async Task<HttpClient> CreateAdminAsync(
        this PantryApiFactory factory, string displayName = "Admin example", SoftwareAuthenticator? authenticator = null)
    {
        using var throwaway = authenticator is null ? new SoftwareAuthenticator(PantryApiFactory.Origin) : null;
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/setup");
        var options = await client.PostAsJsonAsync("/api/setup/passkey-options", new { displayName });
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var credential = (authenticator ?? throwaway!).Create(await options.Content.ReadFromJsonAsync<JsonElement>());
        var complete = await client.PostAsJsonAsync("/api/setup/complete", new { credential });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return client;
    }
}
