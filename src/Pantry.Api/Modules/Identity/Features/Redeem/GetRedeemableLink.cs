using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Redeem;

/// <summary>What the redeem screen shows before the passkey is created: an invite for a member or a recovery link for a user.</summary>
public static class GetRedeemableLink
{
    public static RouteHandlerBuilder MapGetRedeemableLink(this IEndpointRouteBuilder redeem) => redeem.MapGet("/", HandleAsync);

    private static async Task<Results<Ok<RedeemableLinkResponse>, ProblemHttpResult>> HandleAsync(
        string token, IdentityModuleDbContext db, IHouseholdModule household, TimeProvider time, CancellationToken cancellationToken)
    {
        var (redeemable, problem) = await RedeemableLinks.FindAsync(token, db, household, time.GetUtcNow(), cancellationToken);
        if (redeemable is null)
        {
            return problem!;
        }

        return TypedResults.Ok(new RedeemableLinkResponse(
            redeemable.Link.Kind, redeemable.Name, redeemable.User?.ChosenUserName, redeemable.InvitedBy, redeemable.Link.ExpiresAt));
    }
}

/// <summary>
/// <c>Name</c> is the member's name for an invite (a suggestion for the display name) or the user's display name for recovery.
/// <c>UserName</c> is the recovering user's username; null for an invite or a user who has none yet.
/// </summary>
public sealed record RedeemableLinkResponse(PasskeyLinkKind Kind, string Name, string? UserName, string? InvitedBy, DateTimeOffset ExpiresAt);
