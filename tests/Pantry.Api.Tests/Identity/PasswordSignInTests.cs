using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;
using Pantry.Api.Tests.Infrastructure;
using static Pantry.Api.Tests.Identity.IdentityScenarios;

namespace Pantry.Api.Tests.Identity;

/// <summary>Password sign-in, first run and invites without a passkey, and the lockout (ADR 0018).</summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordSignInTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string Password = "pantry shelf 2026";
    private const string SignInUrl = "/api/auth/sign-in/password";

    private readonly SoftwareAuthenticator _authenticator = new(PantryApiFactory.Origin);

    public Task InitializeAsync() => factory.DeleteAllUsersAsync();

    public Task DisposeAsync()
    {
        _authenticator.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task First_run_with_a_password_creates_the_admin_without_a_passkey()
    {
        // Arrange
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/setup");

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/setup/complete-with-password", new { displayName = " Admin example ", userName = " Admin.Example ", password = Password, uiLanguage = "pl" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Admin example", me.GetProperty("displayName").GetString());
        Assert.Equal("Admin.Example", me.GetProperty("userName").GetString());
        Assert.Equal("admin", me.GetProperty("role").GetString());
        Assert.Equal("pl", me.GetProperty("uiLanguage").GetString());
        Assert.Equal(["password"], SignInMethodsOf(me));
        Assert.Empty(await client.GetFromJsonAsync<JsonElement[]>("/api/me/passkeys") ?? []);
        var members = await client.GetFromJsonAsync<JsonElement>("/api/household/members");
        Assert.Equal(me.GetProperty("id").GetGuid(), members.GetProperty("members")[0].GetProperty("userId").GetGuid());
        Assert.False((await factory.CreateBrowserClient().GetFromJsonAsync<JsonElement>("/api/setup")).GetProperty("firstRunNeeded").GetBoolean());
    }

    [Fact]
    public async Task First_run_with_a_password_happens_once()
    {
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        var late = factory.CreateBrowserClient();
        await late.GetAsync("/api/setup");

        var response = await late.PostAsJsonAsync("/api/setup/complete-with-password", new { displayName = "Late", userName = "late", password = Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("ab", Password)]
    [InlineData("has space", Password)]
    [InlineData("no@sign", Password)]
    [InlineData("thirty-three-characters-username1", Password)]
    [InlineData("0123456789abcdef0123456789ABCDEF", Password)]
    [InlineData("admin", "eleven char")]
    [InlineData("admin", "")]
    public async Task First_run_rejects_an_invalid_username_or_password(string userName, string password)
    {
        var client = factory.CreateBrowserClient();
        await client.GetAsync("/api/setup");

        var response = await client.PostAsJsonAsync("/api/setup/complete-with-password", new { displayName = "Admin", userName, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await factory.CreateBrowserClient().GetFromJsonAsync<JsonElement>("/api/setup")).GetProperty("firstRunNeeded").GetBoolean());
    }

    [Fact]
    public async Task Passwords_need_12_to_128_characters_of_any_kind()
    {
        // Arrange
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        string[] passwords = ["twelve chars", "aaaaaaaaaaaa", "ąęółśżźćń żółw", new string('x', 128)];

        // Act
        var accepted = new List<HttpStatusCode>();
        var current = Password;
        foreach (var password in passwords)
        {
            accepted.Add((await admin.PutAsJsonAsync("/api/me/password", new { currentPassword = current, newPassword = password })).StatusCode);
            current = password;
        }

        var tooLong = await admin.PutAsJsonAsync("/api/me/password", new { currentPassword = current, newPassword = new string('x', 129) });

        // Assert
        Assert.All(accepted, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.True((await tooLong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("NewPassword", out _));
    }

    [Fact]
    public async Task A_user_signs_in_with_username_and_password_in_any_letter_case()
    {
        // Arrange
        await factory.CreateAdminWithPasswordAsync("Kitchen.Admin", Password);
        var client = factory.CreateBrowserClient();

        // Act
        var response = await SignInWithPasswordAsync(client, " kitchen.ADMIN ", Password);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("expires=", SetCookie(response, "pantry.session"), StringComparison.OrdinalIgnoreCase);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Kitchen.Admin", me.GetProperty("userName").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/me/theme", new { theme = "dark" })).StatusCode);
    }

    [Fact]
    public async Task The_sixth_attempt_after_five_wrong_passwords_is_refused_until_the_lockout_ends()
    {
        // Arrange
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        var client = factory.CreateBrowserClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(client, "admin", "wrong password " + attempt)).StatusCode);
        }

        // Act
        var whileLockedOut = await SignInWithPasswordAsync(client, "admin", Password);
        var lockoutEnd = await LockoutEndAsync("admin");
        await EndLockoutAsync("admin");
        var afterLockout = await SignInWithPasswordAsync(client, "admin", Password);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, whileLockedOut.StatusCode);
        Assert.InRange(lockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);
    }

    [Fact]
    public async Task Parallel_wrong_passwords_all_count_towards_the_lockout()
    {
        // Arrange
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        var clients = Enumerable.Range(0, 10).Select(_ => factory.CreateBrowserClient()).ToList();
        foreach (var client in clients)
        {
            await client.GetAsync("/api/antiforgery");
        }

        // Act
        var guesses = await Task.WhenAll(clients.Select((client, i) =>
            client.PostAsJsonAsync(SignInUrl, new { userName = "admin", password = "parallel guess " + i })));
        var withTheRightPassword = await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password);

        // Assert
        Assert.All(guesses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.Equal(HttpStatusCode.Unauthorized, withTheRightPassword.StatusCode);
        Assert.NotNull(await LockoutEndAsync("admin"));
    }

    [Fact]
    public async Task Every_refusal_costs_one_password_hash_except_an_over_long_password()
    {
        // Arrange
        var hasher = new CountingPasswordHasher();
        await using var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPasswordHasher<User>>(hasher)));
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        var lockedId = await RedeemWithPasswordAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin, "Locked")), "locked", Password);
        await LockOutAsync(lockedId);
        var passkeyOnlyId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin, "Passkey")), _authenticator);
        (string UserName, string Password)[] refusals =
        [
            ("nobody", Password),
            ("admin", "not the password"),
            ("locked", Password),
            (passkeyOnlyId.ToString("N"), Password),
        ];

        // Act
        var hashes = new List<int>();
        foreach (var (userName, password) in refusals)
        {
            var before = hasher.Count;
            Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(PantryApiFactory.CreateBrowserClient(app), userName, password)).StatusCode);
            hashes.Add(hasher.Count - before);
        }

        var beforeOverLong = hasher.Count;
        var overLong = await SignInWithPasswordAsync(PantryApiFactory.CreateBrowserClient(app), "admin", new string('x', 129));

        // Assert
        Assert.Equal([1, 1, 1, 1], hashes);
        Assert.Equal(HttpStatusCode.Unauthorized, overLong.StatusCode);
        Assert.Equal(beforeOverLong, hasher.Count);
        Assert.Equal(0, await AccessFailedCountAsync(passkeyOnlyId));
    }

    [Fact]
    public async Task Failed_attempts_count_per_user()
    {
        // Arrange
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        await RedeemWithPasswordAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin)), "partner", Password);

        // Act
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", "wrong password " + attempt);
        }

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "partner", Password)).StatusCode);
    }

    [Fact]
    public async Task A_correct_password_resets_the_failed_attempts()
    {
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", "wrong password " + attempt);
        }

        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
        await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", "wrong password again");

        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", Password)).StatusCode);
    }

    [Fact]
    public async Task Unknown_user_wrong_password_locked_account_and_passkey_only_user_get_the_same_answer()
    {
        // Arrange
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        var lockedId = await RedeemWithPasswordAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin, "Locked")), "locked", Password);
        await LockOutAsync(lockedId);
        var passkeyOnlyId = await RedeemAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin, "Passkey")), _authenticator);

        // Act
        HttpResponseMessage[] responses =
        [
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "nobody", Password),
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "admin", "not the password"),
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "locked", Password),
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), passkeyOnlyId.ToString("N"), Password),
            await SignInWithPasswordAsync(factory.CreateBrowserClient(), "", ""),
        ];

        // Assert
        var bodies = await Task.WhenAll(responses.Select(async r => WithoutTraceId(await r.Content.ReadAsStringAsync())));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.All(responses, r => Assert.False(r.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("pantry.session=", StringComparison.Ordinal))));
        Assert.Single(bodies.Distinct());
        Assert.Equal(0, await AccessFailedCountAsync(passkeyOnlyId));
    }

    [Fact]
    public async Task Password_sign_in_needs_an_antiforgery_token()
    {
        await factory.CreateAdminWithPasswordAsync("admin", Password);
        var client = factory.CreateClient(new() { BaseAddress = PantryApiFactory.BaseAddress });

        var response = await client.PostAsJsonAsync(SignInUrl, new { userName = "admin", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_invite_can_be_redeemed_with_a_password_and_no_passkey()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync();
        var memberId = await AddMemberAsync(admin, "Guest");
        var token = await IssueInviteAsync(admin, memberId);
        var invited = factory.CreateBrowserClient();
        await invited.GetAsync(RedeemUrl(token));

        // Act
        var response = await invited.PostAsJsonAsync(
            $"{RedeemUrl(token)}/complete-with-password", new { displayName = " Guest example ", userName = "guest", password = Password, uiLanguage = "en" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await invited.GetFromJsonAsync<JsonElement>("/api/me");
        Assert.Equal("Guest example", me.GetProperty("displayName").GetString());
        Assert.Equal("guest", me.GetProperty("userName").GetString());
        Assert.Equal("user", me.GetProperty("role").GetString());
        Assert.Equal(["password"], SignInMethodsOf(me));
        var members = await admin.GetFromJsonAsync<JsonElement>("/api/household/members");
        var guest = members.GetProperty("members").EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == memberId);
        Assert.Equal(me.GetProperty("id").GetGuid(), guest.GetProperty("userId").GetGuid());
        Assert.Equal(["password"], guest.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        await AssertGoneAsync(await factory.CreateBrowserClient().GetAsync(RedeemUrl(token)));
        Assert.Equal(HttpStatusCode.OK, (await SignInWithPasswordAsync(factory.CreateBrowserClient(), "guest", Password)).StatusCode);
    }

    [Fact]
    public async Task An_invite_rejects_a_taken_username_in_any_letter_case_and_stays_open()
    {
        var admin = await factory.CreateAdminWithPasswordAsync("admin", Password);
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        var invited = factory.CreateBrowserClient();
        await invited.GetAsync(RedeemUrl(token));

        var response = await CompleteLinkWithPasswordAsync(invited, token, "ADMIN", Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("UserName", out _));
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateBrowserClient().GetAsync(RedeemUrl(token))).StatusCode);
    }

    [Fact]
    public async Task A_used_invite_cannot_be_redeemed_with_a_password()
    {
        var admin = await factory.CreateAdminAsync();
        var token = await IssueInviteAsync(admin, await AddMemberAsync(admin));
        await RedeemAsync(factory.CreateBrowserClient(), token, _authenticator);

        var late = factory.CreateBrowserClient();
        await late.GetAsync("/api/antiforgery");
        var response = await CompleteLinkWithPasswordAsync(late, token, "late", Password);

        await AssertGoneAsync(response);
    }

    [Fact]
    public async Task Members_show_the_sign_in_methods_of_their_users()
    {
        // Arrange
        var admin = await factory.CreateAdminAsync(authenticator: _authenticator);
        await admin.PostAsJsonAsync("/api/me/password", new { userName = "admin", password = Password });
        await RedeemWithPasswordAsync(factory.CreateBrowserClient(), await IssueInviteAsync(admin, await AddMemberAsync(admin, "Partner")), "partner", Password);
        await AddMemberAsync(admin, "Toddler");

        // Act
        var members = (await admin.GetFromJsonAsync<JsonElement>("/api/household/members")).GetProperty("members").EnumerateArray().ToList();

        // Assert
        Assert.Equal(["passkey", "password"], members[0].GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(["password"], members[1].GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(JsonValueKind.Null, members[2].GetProperty("signInMethods").ValueKind);
    }

    private static IEnumerable<string?> SignInMethodsOf(JsonElement user) =>
        user.GetProperty("signInMethods").EnumerateArray().Select(m => m.GetString());

    private static async Task AssertGoneAsync(HttpResponseMessage response) =>
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);

    private static string WithoutTraceId(string problem)
    {
        var json = JsonNode.Parse(problem)!.AsObject();
        json.Remove("traceId");
        return json.ToJsonString();
    }

    private async Task<DateTimeOffset?> LockoutEndAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        return await db.Users.Where(u => u.NormalizedUserName == userName.ToUpperInvariant()).Select(u => u.LockoutEnd).SingleAsync();
    }

    private async Task EndLockoutAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Users
            .Where(u => u.NormalizedUserName == userName.ToUpperInvariant())
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    private async Task LockOutAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddMinutes(15)));
    }

    /// <summary>Identity's hasher, counting every hash and verification.</summary>
    private sealed class CountingPasswordHasher : IPasswordHasher<User>
    {
        private readonly PasswordHasher<User> _inner = new();
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public string HashPassword(User user, string password)
        {
            Interlocked.Increment(ref _count);
            return _inner.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            Interlocked.Increment(ref _count);
            return _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private async Task<int> AccessFailedCountAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Users
            .Where(u => u.Id == userId).Select(u => u.AccessFailedCount).SingleAsync();
    }
}
