using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Identity;

internal static class IdentityScenarios
{
    /// <summary>The SPA's sign-in: fetch a token, request options, answer them with the passkey, post the assertion.</summary>
    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, SoftwareAuthenticator authenticator)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/antiforgery")).StatusCode);
        var options = await client.PostAsync("/api/auth/passkey-options", null);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var credential = authenticator.Get(await options.Content.ReadFromJsonAsync<JsonElement>());
        return await client.PostAsJsonAsync("/api/auth/sign-in", new { credential });
    }

    /// <summary>The SPA's password sign-in: fetch a token, post the username and password.</summary>
    public static async Task<HttpResponseMessage> SignInWithPasswordAsync(HttpClient client, string userName, string password)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/antiforgery")).StatusCode);
        return await client.PostAsJsonAsync("/api/auth/sign-in/password", new { userName, password });
    }

    public static Task<HttpResponseMessage> CompleteLinkWithPasswordAsync(
        HttpClient client, string token, string? userName, string password, string displayName = "Invited example") =>
        client.PostAsJsonAsync($"{RedeemUrl(token)}/complete-with-password", new { displayName, userName, password });

    /// <summary>Opens the link on <paramref name="client"/>, redeems it with a password and returns the signed-in user's id.</summary>
    public static async Task<Guid> RedeemWithPasswordAsync(HttpClient client, string token, string? userName, string password)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(RedeemUrl(token))).StatusCode);
        var response = await CompleteLinkWithPasswordAsync(client, token, userName, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> AddMemberAsync(HttpClient admin, string name = "Guest")
    {
        var response = await admin.PostAsJsonAsync("/api/household/members", new { name, portionFactor = 1m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Issues an invite for <paramref name="memberId"/> and returns the token from the link.</summary>
    public static async Task<string> IssueInviteAsync(HttpClient admin, Guid memberId)
    {
        var response = await admin.PostAsJsonAsync("/api/passkey-links/invites", new { memberId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    public static async Task<string> IssueRecoveryLinkAsync(HttpClient admin, Guid userId)
    {
        var response = await admin.PostAsJsonAsync("/api/passkey-links/recovery", new { userId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    public static string RedeemUrl(string token) => $"/api/passkey-links/redeem/{token}";

    /// <summary>The SPA's redeem screen: open the link, request creation options, create the passkey.</summary>
    public static async Task<JsonObject> CreateLinkPasskeyAsync(
        HttpClient client, string token, SoftwareAuthenticator authenticator, string displayName = "Invited example")
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(RedeemUrl(token))).StatusCode);
        var options = await client.PostAsJsonAsync($"{RedeemUrl(token)}/passkey-options", new { displayName });
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        return authenticator.Create(await options.Content.ReadFromJsonAsync<JsonElement>());
    }

    public static Task<HttpResponseMessage> CompleteLinkAsync(HttpClient client, string token, JsonObject credential) =>
        client.PostAsJsonAsync($"{RedeemUrl(token)}/complete", new { credential });

    /// <summary>Redeems the link on <paramref name="client"/> and returns the signed-in user's id.</summary>
    public static async Task<Guid> RedeemAsync(HttpClient client, string token, SoftwareAuthenticator authenticator, string displayName = "Invited example")
    {
        var response = await CompleteLinkAsync(client, token, await CreateLinkPasskeyAsync(client, token, authenticator, displayName));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{name}=", StringComparison.Ordinal));

    public static void AssertCleared(HttpResponseMessage response, string name) =>
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookie(response, name), StringComparison.OrdinalIgnoreCase);
}
