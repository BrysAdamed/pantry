using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

/// <summary>
/// The password in My account and in recovery links, and the rule that the last sign-in method stays. Two browser clients
/// of one user stand for two devices; the test app checks sessions on every request.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MyPasswordTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string PasswordUrl = "/api/me/password";
    private const string Password = "pantry shelf 2026";
    private const string NewPassword = "a brand new pantry key";

    private readonly SoftwareAuthenticator _phone = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _phone.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_passkey_user_sets_a_password_with_a_username_and_other_devices_are_signed_out()
    {
        // Arrange
        var phone = await factory.CreateAdminAsync(authenticator: _phone);
        var laptop = await SignedInWithPasskeyAsync();

        // Act
        var withoutUserName = await phone.PostAsJsonAsync(PasswordUrl, new { password = Password });
        var response = await phone.PostAsJsonAsync(PasswordUrl, new { userName = "admin", password = Password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, withoutUserName.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", me.GetProperty("userName").GetString());
        Assert.Equal(["passkey", "password"], me.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await phone.PutAsJsonAsync("/api/me/theme", new { theme = "dark" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(factory.CreateBrowserClient(), _phone)).StatusCode);
    }

    [Fact]
    public async Task A_password_is_set_once_and_the_username_is_chosen_once()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync(authenticator: _phone);
        await admin.PostAsJsonAsync(PasswordUrl, new { userName = "admin", password = Password });

        // Act
        var again = await admin.PostAsJsonAsync(PasswordUrl, new { userName = "other", password = NewPassword });
        await admin.DeleteAsync(PasswordUrl);
        var afterRemoval = await admin.PostAsJsonAsync(PasswordUrl, new { userName = "other", password = NewPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRemoval.StatusCode);
        Assert.Equal("admin", (await afterRemoval.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userName").GetString());
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Setting_a_password_rejects_a_taken_username()
    {
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        var user = factory.CreateBrowserClient();
        await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _phone);

        var response = await user.PostAsJsonAsync(PasswordUrl, new { userName = "Admin", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("UserName", out _));
        Assert.Equal(JsonValueKind.Null, (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("userName").ValueKind);
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_signs_out_other_devices()
    {
        // Arrange
        var phone = await factory.CreateAdminWithPasswordAsync("admin", Password);
        var laptop = await SignedInWithPasswordAsync("admin", Password);

        // Act
        var wrongCurrent = await phone.PutAsJsonAsync(PasswordUrl, new { currentPassword = "not the password", newPassword = NewPassword });
        var stillSignedIn = await laptop.GetAsync("/api/me");
        var changed = await phone.PutAsJsonAsync(PasswordUrl, new { currentPassword = Password, newPassword = NewPassword });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.True((await wrongCurrent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("CurrentPassword", out _));
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Removing_the_password_signs_out_other_devices_and_keeps_the_username()
    {
        // Arrange
        var phone = await factory.CreateAdminAsync(authenticator: _phone);
        await phone.PostAsJsonAsync(PasswordUrl, new { userName = "admin", password = Password });
        var laptop = await SignedInWithPasswordAsync("admin", Password);

        // Act
        var response = await phone.DeleteAsync(PasswordUrl);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin", me.GetProperty("userName").GetString());
        Assert.Equal(["passkey"], me.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await phone.DeleteAsync(PasswordUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await phone.PutAsJsonAsync(PasswordUrl, new { currentPassword = Password, newPassword = NewPassword })).StatusCode);
    }

    [Fact]
    public async Task The_password_of_a_user_without_a_passkey_cannot_be_removed()
    {
        var client = await factory.CreateAdminWithPasswordAsync("admin", Password);

        var response = await client.DeleteAsync(PasswordUrl);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The last sign-in method cannot be removed.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
    }

    [Fact]
    public async Task The_last_passkey_can_go_when_a_password_is_left_and_then_the_password_has_to_stay()
    {
        // Arrange
        var client = await factory.CreateAdminAsync(authenticator: _phone);
        var lastPasskey = $"/api/me/passkeys/{Base64Url(_phone.CredentialId)}";
        var refused = await client.DeleteAsync(lastPasskey);
        await client.PostAsJsonAsync(PasswordUrl, new { userName = "admin", password = Password });

        // Act
        var removed = await client.DeleteAsync(lastPasskey);
        var passwordRemoved = await client.DeleteAsync(PasswordUrl);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, passwordRemoved.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal(["password"], me.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task Removing_the_password_and_the_last_passkey_at_once_keeps_one_method()
    {
        var client = await factory.CreateAdminAsync(authenticator: _phone);
        await client.PostAsJsonAsync(PasswordUrl, new { userName = "admin", password = Password });

        var responses = await Task.WhenAll(
            client.DeleteAsync(PasswordUrl),
            client.DeleteAsync($"/api/me/passkeys/{Base64Url(_phone.CredentialId)}"));

        Assert.Single(responses, r => r.IsSuccessStatusCode);
        Assert.Equal(1, await SignInMethodCountAsync());
    }

    [Fact]
    public async Task A_recovery_link_lets_a_password_only_user_set_a_new_password()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var oldDevice = factory.CreateBrowserClient();
        var userId = await RedeemWithPasswordAsync(oldDevice, await IssueInviteAsync(admin, await AddMemberAsync(admin)), "partner", Password);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "partner", "forgotten password " + attempt);
        }

        var token = await IssueRecoveryLinkAsync(admin, userId);
        var newDevice = factory.CreateBrowserClient();

        // Act
        var link = await newDevice.GetFromJsonAsync<JsonElement>(RedeemUrl(token));
        var recovered = await CompleteLinkWithPasswordAsync(newDevice, token, userName: "ignored", NewPassword);

        // Assert
        Assert.Equal("partner", link.GetProperty("userName").GetString());
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var me = await recovered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, me.GetProperty("id").GetGuid());
        Assert.Equal("partner", me.GetProperty("userName").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldDevice.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newDevice.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "partner", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "partner", NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await factory.CreateBrowserClient().GetAsync(RedeemUrl(token))).StatusCode);
    }

    [Fact]
    public async Task A_recovery_link_gives_a_passkey_user_a_password_with_a_new_username()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var userId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin)), _phone);
        var token = await IssueRecoveryLinkAsync(admin, userId);
        var client = factory.CreateBrowserClient();

        // Act
        var link = await client.GetFromJsonAsync<JsonElement>(RedeemUrl(token));
        var withoutUserName = await CompleteLinkWithPasswordAsync(client, token, userName: null, NewPassword);
        var recovered = await CompleteLinkWithPasswordAsync(client, token, "partner", NewPassword);

        // Assert
        Assert.Equal(JsonValueKind.Null, link.GetProperty("userName").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, withoutUserName.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var me = await recovered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["passkey", "password"], me.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "partner", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task A_recovery_link_redeemed_with_a_passkey_also_signs_out_other_devices()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var lostPhone = factory.CreateBrowserClient();
        var userId = await RedeemAsync(lostPhone, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _phone);
        using var newPhone = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var newDevice = factory.CreateBrowserClient();

        // Act
        await RedeemAsync(newDevice, await IssueRecoveryLinkAsync(admin, userId), newPhone);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await lostPhone.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newDevice.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task The_password_endpoints_need_a_session()
    {
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        var anonymous = factory.CreateBrowserClient();
        await anonymous.GetAsync("/api/antiforgery");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(PasswordUrl, new { userName = "x", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(PasswordUrl, new { currentPassword = Password, newPassword = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(PasswordUrl)).StatusCode);
    }

    private async Task<HttpClient> SignedInWithPasskeyAsync()
    {
        var client = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, _phone)).StatusCode);
        return client;
    }

    private async Task<HttpClient> SignedInWithPasswordAsync(string userName, string password)
    {
        var client = factory.CreateBrowserClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(client, userName, password)).StatusCode);
        return client;
    }

    private async Task<int> SignInMethodCountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = await db.Users.SingleAsync();
        return await db.UserPasskeys.CountAsync(p => p.UserId == user.Id) + (user.PasswordHash is null ? 0 : 1);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
