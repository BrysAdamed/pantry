using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

/// <summary>
/// My account: preferences that follow the user to every device, and the user's own passkeys. Two browser clients
/// signed in with the same passkey stand for two devices. The test app checks sessions on every request.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyAccountTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string PasskeysUrl = "/api/me/passkeys";

    private readonly SoftwareAuthenticator _phone = new(PantryApiFactory.Origin);
    private readonly SoftwareAuthenticator _laptop = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _phone.Dispose();
        _laptop.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Preferences_chosen_on_one_device_apply_on_another()
    {
        // Arrange
        var phone = await factory.CreateAdminAsync(uiLanguage: "en", authenticator: _phone);
        var laptop = await SignedInDeviceAsync(_phone);

        // Act
        var language = await phone.PutAsJsonAsync("/api/me/ui-language", new { uiLanguage = "pl" });
        var theme = await phone.PutAsJsonAsync("/api/me/theme", new { theme = "dark" });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, language.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, theme.StatusCode);
        var me = await laptop.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("pl", me.GetProperty("uiLanguage").GetString());
        Assert.Equal("dark", me.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task Changing_language_and_theme_at_the_same_time_keeps_both()
    {
        var client = await factory.CreateAdminAsync();

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync("/api/me/ui-language", new { uiLanguage = "pl" }),
            client.PutAsJsonAsync("/api/me/theme", new { theme = "light" }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("pl", me.GetProperty("uiLanguage").GetString());
        Assert.Equal("light", me.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task Unknown_or_missing_preferences_are_rejected()
    {
        var client = await factory.CreateAdminAsync();

        var unknownLanguage = await client.PutAsJsonAsync("/api/me/ui-language", new { uiLanguage = "de" });
        var missingLanguage = await client.PutAsJsonAsync("/api/me/ui-language", new { });
        var unknownTheme = await client.PutAsJsonAsync("/api/me/theme", new { theme = "sepia" });
        var missingTheme = await client.PutAsJsonAsync("/api/me/theme", new { });
        var numericLanguage = await client.PutAsJsonAsync("/api/me/ui-language", new { uiLanguage = 1 });
        var numericTheme = await client.PutAsJsonAsync("/api/me/theme", new { theme = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, unknownLanguage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingLanguage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownTheme.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingTheme.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, numericLanguage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, numericTheme.StatusCode);
    }

    [Fact]
    public async Task First_run_and_invites_keep_the_ui_language_of_the_screen()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync(uiLanguage: "pl");
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var invited = factory.CreateBrowserClient();

        // Act
        var credential = await CreateLinkPasskeyAsync(invited, token, _laptop);
        var redeemed = await invited.PostAsJsonAsync($"{RedeemUrl(token)}/complete", new { credential, uiLanguage = "en" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        Assert.Equal("pl", (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("uiLanguage").GetString());
        Assert.Equal("en", (await invited.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("uiLanguage").GetString());
    }

    [Fact]
    public async Task Passkeys_are_listed_with_when_they_were_created_and_last_used()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var phone = await factory.CreateAdminAsync(authenticator: _phone);

        // Act
        var afterFirstRun = await PasskeysAsync(phone);
        await SignedInDeviceAsync(_phone);
        var afterSignIn = await PasskeysAsync(phone);

        // Assert
        var passkey = Assert.Single(afterFirstRun);
        Assert.Equal(JsonValueKind.Null, passkey.GetProperty("name").ValueKind);
        Assert.InRange(passkey.GetProperty("createdAt").GetDateTimeOffset(), before, DateTimeOffset.UtcNow.AddMinutes(1));
        var firstUse = passkey.GetProperty("lastUsedAt").GetDateTimeOffset();
        Assert.True(Assert.Single(afterSignIn).GetProperty("lastUsedAt").GetDateTimeOffset() >= firstUse);
        Assert.Equal(Base64Url(_phone.CredentialId), passkey.GetProperty("id").GetString());
    }

    [Fact]
    public async Task A_user_adds_a_passkey_and_signs_in_with_it()
    {
        // Arrange
        var phone = await factory.CreateAdminAsync(authenticator: _phone);

        // Act
        var added = await AddPasskeyAsync(phone, _laptop, "Laptop");

        // Assert
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var passkey = await added.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Laptop", passkey.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, passkey.GetProperty("lastUsedAt").ValueKind);
        Assert.Equal(2, (await PasskeysAsync(phone)).Length);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(factory.CreateBrowserClient(), _laptop)).StatusCode);
    }

    [Fact]
    public async Task Adding_a_passkey_needs_a_ceremony_and_a_short_name()
    {
        var client = await factory.CreateAdminAsync();
        var options = await client.PostAsync($"{PasskeysUrl}/options", null);
        var credential = _laptop.Create(await options.Content.ReadFromJsonAsync<JsonElement>());

        var tooLong = await client.PostAsJsonAsync(PasskeysUrl, new { credential, name = new string('x', 51) });
        var noCredential = await client.PostAsJsonAsync(PasskeysUrl, new { });

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noCredential.StatusCode);
    }

    [Fact]
    public async Task A_passkey_can_be_renamed_only_by_its_owner()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync(authenticator: _phone);
        var id = Base64Url(_phone.CredentialId);
        var user = factory.CreateBrowserClient();
        await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _laptop);

        // Act
        var renamed = await admin.PutAsJsonAsync($"{PasskeysUrl}/{id}/name", new { name = "  Phone  " });
        var empty = await admin.PutAsJsonAsync($"{PasskeysUrl}/{id}/name", new { name = " " });
        var someoneElses = await user.PutAsJsonAsync($"{PasskeysUrl}/{id}/name", new { name = "Mine" });
        var unknown = await admin.PutAsJsonAsync($"{PasskeysUrl}/{Base64Url([1, 2, 3])}/name", new { name = "Phone" });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, someoneElses.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Phone", Assert.Single(await PasskeysAsync(admin)).GetProperty("name").GetString());
    }

    [Fact]
    public async Task The_last_passkey_cannot_be_removed()
    {
        var client = await factory.CreateAdminAsync(authenticator: _phone);

        var response = await client.DeleteAsync($"{PasskeysUrl}/{Base64Url(_phone.CredentialId)}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await PasskeysAsync(client));
    }

    [Fact]
    public async Task Removing_a_lost_passkey_ends_the_other_sessions_and_keeps_this_one()
    {
        // Arrange
        var laptop = await factory.CreateAdminAsync(authenticator: _laptop);
        var lostPhone = factory.CreateBrowserClient();
        await AddPasskeyAsync(laptop, _phone, "Phone");
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(lostPhone, _phone)).StatusCode);

        // Act
        var removed = await laptop.DeleteAsync($"{PasskeysUrl}/{Base64Url(_phone.CredentialId)}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await lostPhone.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await laptop.PutAsJsonAsync("/api/me/theme", new { theme = "dark" })).StatusCode);
        Assert.Single(await PasskeysAsync(laptop));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(factory.CreateBrowserClient(), _phone)).StatusCode);
    }

    [Fact]
    public async Task Removing_two_passkeys_at_once_keeps_one()
    {
        var client = await factory.CreateAdminAsync(authenticator: _phone);
        await AddPasskeyAsync(client, _laptop, "Laptop");

        var responses = await Task.WhenAll(
            client.DeleteAsync($"{PasskeysUrl}/{Base64Url(_phone.CredentialId)}"),
            client.DeleteAsync($"{PasskeysUrl}/{Base64Url(_laptop.CredentialId)}"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single(await PasskeysAsync(await SignedInDeviceAsync(_phone, _laptop)));
    }

    [Fact]
    public async Task Removing_passkeys_at_the_same_time_never_fails_with_a_server_error()
    {
        // Arrange
        using var tablet = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var client = await factory.CreateAdminAsync(authenticator: _phone);
        await AddPasskeyAsync(client, _laptop, "Laptop");
        await AddPasskeyAsync(client, tablet, "Tablet");

        // Act
        var responses = await Task.WhenAll(
            client.DeleteAsync($"{PasskeysUrl}/{Base64Url(_phone.CredentialId)}"),
            client.DeleteAsync($"{PasskeysUrl}/{Base64Url(_laptop.CredentialId)}"));

        // Assert
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized }));
        var removed = responses.Count(r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.InRange(removed, 1, 2);
        Assert.Equal(3 - removed, (await PasskeysAsync(await SignedInDeviceAsync(tablet))).Length);
    }

    [Fact]
    public async Task A_user_cannot_remove_someone_elses_passkey()
    {
        var admin = await factory.CreateAdminAsync(authenticator: _phone);
        await AddPasskeyAsync(admin, _laptop, "Laptop");
        var user = factory.CreateBrowserClient();
        using var userPhone = new SoftwareAuthenticator(PantryApiFactory.Origin);
        await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), userPhone);

        var response = await user.DeleteAsync($"{PasskeysUrl}/{Base64Url(_laptop.CredentialId)}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, (await PasskeysAsync(admin)).Length);
    }

    [Fact]
    public async Task Signing_out_other_sessions_keeps_this_one()
    {
        var phone = await factory.CreateAdminAsync(authenticator: _phone);
        var laptop = await SignedInDeviceAsync(_phone);

        var response = await phone.PostAsync("/api/me/sign-out-others", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync(PasskeysUrl)).StatusCode);
    }

    [Fact]
    public async Task My_account_needs_a_session()
    {
        await factory.CreateAdminAsync();
        var anonymous = factory.CreateBrowserClient();
        await anonymous.GetAsync("/api/antiforgery");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(PasskeysUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"{PasskeysUrl}/options", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/me/theme", new { theme = "dark" })).StatusCode);
    }

    private async Task<HttpClient> SignedInDeviceAsync(params SoftwareAuthenticator[] candidates)
    {
        foreach (var authenticator in candidates)
        {
            var client = factory.CreateBrowserClient();
            if ((await SignInAsync(client, authenticator)).StatusCode == HttpStatusCode.OK)
            {
                return client;
            }
        }

        throw new InvalidOperationException("No passkey signed in.");
    }

    private static async Task<HttpResponseMessage> AddPasskeyAsync(HttpClient client, SoftwareAuthenticator authenticator, string name)
    {
        var options = await client.PostAsync($"{PasskeysUrl}/options", null);
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var credential = authenticator.Create(await options.Content.ReadFromJsonAsync<JsonElement>());
        return await client.PostAsJsonAsync(PasskeysUrl, new { credential, name });
    }

    private static async Task<JsonElement[]> PasskeysAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement[]>(PasskeysUrl) ?? [];

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
