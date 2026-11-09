using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Redeem;

/// <summary>An open link with what the redeem screen shows: the member's or user's name and who issued it.</summary>
internal sealed record RedeemableLink(PasskeyLink Link, string Name, string? InvitedBy, User? User);

internal static class RedeemableLinks
{
    /// <summary>
    /// Finds the open link for <paramref name="token"/>. An unknown token answers 404; an expired, used or revoked link
    /// answers 410 with its <c>state</c>. An invite whose member was deleted or got a user meanwhile counts as revoked.
    /// </summary>
    public static async Task<(RedeemableLink? Link, ProblemHttpResult? Problem)> FindAsync(
        string token, IdentityModuleDbContext db, IHouseholdModule household, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hash = PasskeyLink.Hash(token);
        var link = await db.PasskeyLinks.SingleOrDefaultAsync(l => l.TokenHash == hash, cancellationToken);
        if (link is null)
        {
            return (null, TypedResults.Problem(title: "The link does not exist.", statusCode: StatusCodes.Status404NotFound));
        }

        var state = link.StateAt(now);
        if (state != PasskeyLinkState.Open)
        {
            return (null, Gone(state));
        }

        var invitedBy = link.CreatedByUserId is { } creatorId
            ? await db.Users.Where(u => u.Id == creatorId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken)
            : null;

        if (link.Kind == PasskeyLinkKind.Invite)
        {
            var member = (await household.GetMembersAsync(cancellationToken)).FirstOrDefault(m => m.Id == link.MemberId);
            return member is null || member.UserId is not null
                ? (null, Gone(PasskeyLinkState.Revoked))
                : (new RedeemableLink(link, member.Name, invitedBy, User: null), null);
        }

        var user = await db.Users.SingleAsync(u => u.Id == link.UserId, cancellationToken);
        return (new RedeemableLink(link, user.DisplayName, invitedBy, user), null);
    }

    /// <summary>
    /// What a recovery link does besides the new passkey or password: ends the user's sessions on every other device,
    /// which may be the lost one, and lifts a password lockout.
    /// </summary>
    public static async Task RecoverAsync(User user, UserManager<User> userManager)
    {
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        if (await userManager.IsLockedOutAsync(user))
        {
            EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, null));
        }

        await OtherSessions.RevokeAsync(user, userManager);
    }

    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Redeeming the link failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    public static ProblemHttpResult Gone(PasskeyLinkState state) => TypedResults.Problem(
        title: "The link cannot be used.",
        statusCode: StatusCodes.Status410Gone,
        extensions: new Dictionary<string, object?> { ["state"] = JsonNamingPolicy.CamelCase.ConvertName(state.ToString()) });
}
