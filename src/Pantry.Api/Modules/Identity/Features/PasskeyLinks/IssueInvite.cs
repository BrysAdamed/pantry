using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.PasskeyLinks;

/// <summary>Issues an invite for a member without a user. Revokes the member's previous invite.</summary>
public static class IssueInvite
{
    public static RouteHandlerBuilder MapIssueInvite(this IEndpointRouteBuilder links) => links.MapPost("/invites", HandleAsync);

    private static async Task<Results<Created<IssuedPasskeyLinkResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        IssueInviteRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        IHouseholdModule household,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var member = (await household.GetMembersAsync(cancellationToken)).FirstOrDefault(m => m.Id == request.MemberId);
        if (member is null)
        {
            return TypedResults.NotFound();
        }

        if (member.UserId is not null)
        {
            return TypedResults.Problem(title: "The member already has an account.", statusCode: StatusCodes.Status409Conflict);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var (link, token) = PasskeyLink.CreateInvite(member.Id, PasskeyLinkIssuing.CurrentUserId(httpContext.User), time.GetUtcNow());
        await PasskeyLinkIssuing.ReplaceAsync(db, l => l.MemberId == member.Id, link, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created($"/api/passkey-links/{link.Id}", IssuedPasskeyLinkResponse.From(link, token));
    }
}

public sealed record IssueInviteRequest(Guid MemberId);
