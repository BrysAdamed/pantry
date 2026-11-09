using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

/// <summary>Removing users and changing roles. The test app checks sessions on every request (validation interval zero).</summary>
[Collection(ApiCollection.Name)]
public sealed class UserAdministrationTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string UsersUrl = "/api/users";
    private const string MembersUrl = "/api/household/members";

    private readonly SoftwareAuthenticator _authenticator = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _authenticator.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Removing_a_user_keeps_the_member_and_ends_their_sessions()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin, "Guest");
        var user = factory.CreateBrowserClient();
        var userId = await RedeemAsync(user, await IssueInviteAsync(admin, memberId), _authenticator);
        await IssueRecoveryLinkAsync(admin, userId);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/me")).StatusCode);

        // Act
        var response = await admin.DeleteAsync($"{UsersUrl}/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(factory.CreateBrowserClient(), _authenticator)).StatusCode);

        var guest = (await admin.GetFromJsonAsync<JsonElement>(MembersUrl)).GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId);
        Assert.Equal(JsonValueKind.Null, guest.GetProperty("account").ValueKind);
        Assert.Equal(JsonValueKind.Null, guest.GetProperty("userId").ValueKind);
        Assert.Empty(await admin.GetFromJsonAsync<JsonElement[]>("/api/passkey-links") ?? []);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        Assert.False(await db.UserPasskeys.AnyAsync(p => p.UserId == userId));
    }

    [Fact]
    public async Task A_removed_users_member_can_be_invited_again()
    {
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin, "Guest");
        var userId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, memberId), _authenticator);
        await admin.DeleteAsync($"{UsersUrl}/{userId}");
        using var newPhone = new SoftwareAuthenticator(PantryApiFactory.Origin);

        var newUserId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, memberId), newPhone);

        Assert.NotEqual(userId, newUserId);
    }

    [Fact]
    public async Task The_last_admin_cannot_be_removed_or_demoted()
    {
        var admin = await factory.CreateAdminAsync();
        var adminId = await MyIdAsync(admin);

        var removed = await admin.DeleteAsync($"{UsersUrl}/{adminId}");
        var demoted = await admin.PutAsJsonAsync($"{UsersUrl}/{adminId}/role", new { role = "user" });

        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, demoted.StatusCode);
        Assert.Equal("admin", (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Promoting_a_user_gives_them_admin_rights_at_once_and_demoting_takes_them_away()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var user = factory.CreateBrowserClient();
        var userId = await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _authenticator);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/passkey-links")).StatusCode);

        // Act
        var promoted = await admin.PutAsJsonAsync($"{UsersUrl}/{userId}/role", new { role = "admin" });
        var asAdmin = await user.GetAsync("/api/passkey-links");
        var demoted = await admin.PutAsJsonAsync($"{UsersUrl}/{userId}/role", new { role = "user" });
        var asUser = await user.GetAsync("/api/passkey-links");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, demoted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
    }

    [Fact]
    public async Task An_admin_can_step_down_or_be_removed_while_another_admin_remains()
    {
        // Arrange
        var first = await factory.CreateAdminAsync();
        var firstId = await MyIdAsync(first);
        var second = factory.CreateBrowserClient();
        var secondId = await RedeemAsync(second, await IssueInviteAsync(first, await AddMemberAsync(first)), _authenticator);
        await first.PutAsJsonAsync($"{UsersUrl}/{secondId}/role", new { role = "admin" });

        // Act
        var steppedDown = await first.PutAsJsonAsync($"{UsersUrl}/{firstId}/role", new { role = "user" });
        var removeLast = await second.DeleteAsync($"{UsersUrl}/{secondId}");
        var removeFirst = await second.DeleteAsync($"{UsersUrl}/{firstId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, steppedDown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, removeLast.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeFirst.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_demotions_of_two_admins_leave_one_admin()
    {
        // Arrange
        var first = await factory.CreateAdminAsync();
        var firstId = await MyIdAsync(first);
        var secondId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(first, await AddMemberAsync(first)), _authenticator);
        await first.PutAsJsonAsync($"{UsersUrl}/{secondId}/role", new { role = "admin" });

        // Act
        var responses = await Task.WhenAll(
            first.PutAsJsonAsync($"{UsersUrl}/{firstId}/role", new { role = "user" }),
            first.PutAsJsonAsync($"{UsersUrl}/{secondId}/role", new { role = "user" }));

        // Assert
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Role == Modules.Identity.Domain.UserRole.Admin));
    }

    [Fact]
    public async Task Unknown_users_and_roles_are_rejected()
    {
        var admin = await factory.CreateAdminAsync();
        var adminId = await MyIdAsync(admin);

        var removed = await admin.DeleteAsync($"{UsersUrl}/{Guid.NewGuid()}");
        var changed = await admin.PutAsJsonAsync($"{UsersUrl}/{Guid.NewGuid()}/role", new { role = "admin" });
        var invalid = await admin.PutAsJsonAsync($"{UsersUrl}/{adminId}/role", new { role = "owner" });
        var missing = await admin.PutAsJsonAsync($"{UsersUrl}/{adminId}/role", new { });

        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task User_administration_is_for_admins_only()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var adminId = await MyIdAsync(admin);
        var user = factory.CreateBrowserClient();
        var userId = await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _authenticator);

        // Act
        var remove = await user.DeleteAsync($"{UsersUrl}/{adminId}");
        var promoteSelf = await user.PutAsJsonAsync($"{UsersUrl}/{userId}/role", new { role = "admin" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promoteSelf.StatusCode);
        Assert.Equal("user", (await user.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/me")).StatusCode);
    }

    private static async Task<Guid> MyIdAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
}
