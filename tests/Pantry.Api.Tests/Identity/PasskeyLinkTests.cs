using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Persistence;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

/// <summary>Invites and recovery links: issuing, listing, revoking and redeeming them with a real passkey attestation.</summary>
[Collection(ApiCollection.Name)]
public sealed class PasskeyLinkTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string LinksUrl = "/api/passkey-links";

    private readonly SoftwareAuthenticator _authenticator = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _authenticator.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Invite_shows_who_it_is_for_and_who_sent_it()
    {
        var admin = await factory.CreateAdminAsync("Admin example");
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin, "Guest"));

        var link = await factory.CreateBrowserClient().GetFromJsonAsync<JsonElement>(RedeemUrl(token));

        Assert.Equal("invite", link.GetProperty("kind").GetString());
        Assert.Equal("Guest", link.GetProperty("name").GetString());
        Assert.Equal("Admin example", link.GetProperty("invitedBy").GetString());
    }

    [Fact]
    public async Task Accepting_an_invite_creates_the_user_links_the_member_and_signs_them_in()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin, "Guest");
        var token = await IssueInviteAsync(admin, memberId);
        var invited = factory.CreateBrowserClient();

        // Act
        var response = await CompleteLinkAsync(invited, token, await CreateLinkPasskeyAsync(invited, token, _authenticator, "  Guest example "));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Guest example", user.GetProperty("displayName").GetString());
        Assert.Equal("user", user.GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.OK, (await invited.GetAsync("/api/me")).StatusCode);

        var members = await admin.GetFromJsonAsync<JsonElement>("/api/household/members");
        var guest = members.GetProperty("members").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == memberId);
        Assert.Equal("user", guest.GetProperty("account").GetString());
        Assert.Equal(user.GetProperty("id").GetGuid(), guest.GetProperty("userId").GetGuid());

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(factory.CreateBrowserClient(), _authenticator)).StatusCode);
    }

    [Fact]
    public async Task Links_are_valid_for_48_hours_and_store_only_a_hash_of_the_token()
    {
        var admin = await factory.CreateAdminAsync();

        var response = await admin.PostAsJsonAsync($"{LinksUrl}/invites", new { memberId = await AddMemberAsync(admin) });

        var issued = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = issued.GetProperty("token").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var link = await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().PasskeyLinks.SingleAsync();
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)), link.TokenHash);
        Assert.True(token.Length >= 43);
        Assert.Equal(TimeSpan.FromHours(48), link.ExpiresAt - link.CreatedAt);
        Assert.InRange((link.ExpiresAt - issued.GetProperty("expiresAt").GetDateTimeOffset()).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Recovery_link_adds_a_passkey_to_the_existing_user()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var userId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin)), _authenticator);
        var token = await IssueRecoveryLinkAsync(admin, userId);
        using var newPhone = new SoftwareAuthenticator(PantryApiFactory.Origin);

        // Act
        var link = await factory.CreateBrowserClient().GetFromJsonAsync<JsonElement>(RedeemUrl(token));
        var recoveredId = await RedeemAsync(factory.CreateBrowserClient(), token, newPhone, displayName: "ignored");

        // Assert
        Assert.Equal("recovery", link.GetProperty("kind").GetString());
        Assert.Equal("Invited example", link.GetProperty("name").GetString());
        Assert.Equal(userId, recoveredId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.UserPasskeys.CountAsync(p => p.UserId == userId));
        Assert.Equal("Invited example", (await db.Users.SingleAsync(u => u.Id == userId)).DisplayName);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(factory.CreateBrowserClient(), newPhone)).StatusCode);
    }

    [Fact]
    public async Task A_used_link_is_rejected()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var first = factory.CreateBrowserClient();
        await RedeemAsync(first, token, _authenticator);
        var second = factory.CreateBrowserClient();

        // Act
        var opened = await second.GetAsync(RedeemUrl(token));
        var options = await second.PostAsJsonAsync($"{RedeemUrl(token)}/passkey-options", new { displayName = "Someone" });

        // Assert
        await AssertGoneAsync(opened, "used");
        await AssertGoneAsync(options, "used");
    }

    [Fact]
    public async Task An_expired_link_is_rejected()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var invited = factory.CreateBrowserClient();
        var credential = await CreateLinkPasskeyAsync(invited, token, _authenticator);
        await ExpireAllLinksAsync();

        // Act
        var opened = await factory.CreateBrowserClient().GetAsync(RedeemUrl(token));
        var completed = await CompleteLinkAsync(invited, token, credential);

        // Assert
        await AssertGoneAsync(opened, "expired");
        await AssertGoneAsync(completed, "expired");
        Assert.Empty(await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? []);
        Assert.Equal(1, await CountUsersAsync());
    }

    [Fact]
    public async Task A_revoked_link_is_rejected()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var id = (await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl))!.Single().GetProperty("id").GetGuid();

        // Act
        var revoked = await admin.DeleteAsync($"{LinksUrl}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        await AssertGoneAsync(await factory.CreateBrowserClient().GetAsync(RedeemUrl(token)), "revoked");
        Assert.Empty(await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? []);
    }

    [Fact]
    public async Task A_used_link_cannot_be_revoked()
    {
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var id = (await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl))!.Single().GetProperty("id").GetGuid();
        await RedeemAsync(factory.CreateBrowserClient(), token, _authenticator);

        var response = await admin.DeleteAsync($"{LinksUrl}/{id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{LinksUrl}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_token_returns_404()
    {
        await factory.CreateAdminAsync();

        var response = await factory.CreateBrowserClient().GetAsync(RedeemUrl("not-a-real-token"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_new_invite_revokes_the_previous_one_for_the_same_member()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin);
        var first = await IssueInviteAsync(admin, memberId);

        // Act
        var second = await IssueInviteAsync(admin, memberId);

        // Assert
        await AssertGoneAsync(await factory.CreateBrowserClient().GetAsync(RedeemUrl(first)), "revoked");
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateBrowserClient().GetAsync(RedeemUrl(second))).StatusCode);
        var open = Assert.Single(await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? []);
        Assert.Equal(memberId, open.GetProperty("memberId").GetGuid());
    }

    [Fact]
    public async Task A_new_recovery_link_revokes_the_previous_one_for_the_same_user()
    {
        var admin = await factory.CreateAdminAsync();
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
        var first = await IssueRecoveryLinkAsync(admin, adminId);

        await IssueRecoveryLinkAsync(admin, adminId);

        await AssertGoneAsync(await factory.CreateBrowserClient().GetAsync(RedeemUrl(first)), "revoked");
        var open = Assert.Single(await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? []);
        Assert.Equal("recovery", open.GetProperty("kind").GetString());
        Assert.Equal(adminId, open.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Open_links_list_invites_and_recovery_links_without_tokens()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
        var usedMember = await AddMemberAsync(admin, "Used");
        await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, usedMember), _authenticator);
        var openMember = await AddMemberAsync(admin, "Open");
        await IssueInviteAsync(admin, openMember);
        await IssueRecoveryLinkAsync(admin, adminId);

        // Act
        var links = await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? [];

        // Assert
        Assert.Equal(["invite", "recovery"], links.Select(l => l.GetProperty("kind").GetString()));
        Assert.Equal(openMember, links[0].GetProperty("memberId").GetGuid());
        Assert.All(links, l => Assert.False(l.TryGetProperty("token", out _)));
    }

    [Fact]
    public async Task Invites_are_only_for_existing_members_without_a_user()
    {
        var admin = await factory.CreateAdminAsync();
        var adminMember = (await admin.GetFromJsonAsync<JsonElement>("/api/household/members")).GetProperty("members")[0].GetProperty("id").GetGuid();

        var linked = await admin.PostAsJsonAsync($"{LinksUrl}/invites", new { memberId = adminMember });
        var unknownMember = await admin.PostAsJsonAsync($"{LinksUrl}/invites", new { memberId = Guid.NewGuid() });
        var unknownUser = await admin.PostAsJsonAsync($"{LinksUrl}/recovery", new { userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Conflict, linked.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownMember.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownUser.StatusCode);
    }

    [Fact]
    public async Task An_invite_for_a_deleted_member_is_rejected()
    {
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin);
        var token = await IssueInviteAsync(admin, memberId);
        var invited = factory.CreateBrowserClient();
        var credential = await CreateLinkPasskeyAsync(invited, token, _authenticator);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/household/members/{memberId}")).StatusCode);
        var completed = await CompleteLinkAsync(invited, token, credential);

        await AssertGoneAsync(completed, "revoked");
        Assert.Equal(1, await CountUsersAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This display name is far too long to fit into fifty characters")]
    public async Task Invite_rejects_an_invalid_display_name(string displayName)
    {
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var invited = factory.CreateBrowserClient();
        await invited.GetAsync(RedeemUrl(token));

        var response = await invited.PostAsJsonAsync($"{RedeemUrl(token)}/passkey-options", new { displayName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_redeems_of_one_link_create_one_user()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var clients = new[] { factory.CreateBrowserClient(), factory.CreateBrowserClient() };
        using var other = new SoftwareAuthenticator(PantryApiFactory.Origin);
        var credentials = new[]
        {
            await CreateLinkPasskeyAsync(clients[0], token, _authenticator),
            await CreateLinkPasskeyAsync(clients[1], token, other),
        };

        // Act
        var responses = await Task.WhenAll(clients.Select((client, i) => CompleteLinkAsync(client, token, credentials[i])));

        // Assert
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Gone);
        Assert.Equal(2, await CountUsersAsync());
    }

    [Fact]
    public async Task A_recovery_passkey_made_for_someone_else_is_rejected()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
        var inviteToken = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var recoveryToken = await IssueRecoveryLinkAsync(admin, adminId);
        var client = factory.CreateBrowserClient();
        var inviteCredential = await CreateLinkPasskeyAsync(client, inviteToken, _authenticator);

        // Act
        var response = await CompleteLinkAsync(client, recoveryToken, inviteCredential);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateBrowserClient().GetAsync(RedeemUrl(recoveryToken))).StatusCode);
    }

    [Fact]
    public async Task Linking_a_member_rolls_back_with_the_identity_transaction()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin);
        await using var scope = factory.Services.CreateAsyncScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var household = scope.ServiceProvider.GetRequiredService<IHouseholdModule>();

        // Act
        await using (var transaction = await identityDb.Database.BeginTransactionAsync())
        {
            Assert.True(await household.LinkMemberToUserAsync(memberId, Guid.NewGuid(), transaction.GetDbTransaction()));
            await transaction.RollbackAsync();
        }

        // Assert
        var member = await scope.ServiceProvider.GetRequiredService<HouseholdDbContext>().Members.SingleAsync(m => m.Id == memberId);
        Assert.Null(member.UserId);
    }

    [Fact]
    public async Task Link_administration_is_for_admins_only()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("id").GetGuid();
        var user = factory.CreateBrowserClient();
        await RedeemAsync(user, await IssueInviteAsync(admin, await AddMemberAsync(admin)), _authenticator);
        var memberId = await AddMemberAsync(admin, "Toddler");
        var anonymous = factory.CreateBrowserClient();
        await anonymous.GetAsync("/api/antiforgery");

        // Act
        HttpResponseMessage[] asUser =
        [
            await user.GetAsync(LinksUrl),
            await user.PostAsJsonAsync($"{LinksUrl}/invites", new { memberId }),
            await user.PostAsJsonAsync($"{LinksUrl}/recovery", new { userId = adminId }),
            await user.DeleteAsync($"{LinksUrl}/{Guid.NewGuid()}"),
        ];
        var anonymousList = await anonymous.GetAsync(LinksUrl);
        var anonymousInvite = await anonymous.PostAsJsonAsync($"{LinksUrl}/invites", new { memberId });

        // Assert
        Assert.All(asUser, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousInvite.StatusCode);
        Assert.Empty(await admin.GetFromJsonAsync<JsonElement[]>(LinksUrl) ?? []);
    }

    private static async Task AssertGoneAsync(HttpResponseMessage response, string state)
    {
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(state, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
    }

    private async Task ExpireAllLinksAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().PasskeyLinks
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private async Task<int> CountUsersAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Users.CountAsync();
    }
}
