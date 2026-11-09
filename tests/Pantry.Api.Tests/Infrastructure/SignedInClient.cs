using System.Net.Http.Json;
using System.Text.Json;

namespace Pantry.Api.Tests.Infrastructure;

public static class SignedInClient
{
    /// <summary>Runs the first run with a software passkey, in <paramref name="uiLanguage"/> when given, and returns the admin's signed-in browser client.</summary>
    public static async Task<HttpClient> CreateAdminAsync(this PantryApiFactory factory, string displayName = "Admin example", string? uiLanguage = null)
    {
        var client = factory.CreateBrowserClient();
        using var authenticator = new SoftwareAuthenticator(PantryApiFactory.Origin);
        await client.GetAsync("/api/setup");
        var optionsResponse = await client.PostAsJsonAsync("/api/setup/passkey-options", new { displayName });
        optionsResponse.EnsureSuccessStatusCode();
        var credential = authenticator.Create(await optionsResponse.Content.ReadFromJsonAsync<JsonElement>());
        (await client.PostAsJsonAsync("/api/setup/complete", new { credential, uiLanguage })).EnsureSuccessStatusCode();
        return client;
    }
}
