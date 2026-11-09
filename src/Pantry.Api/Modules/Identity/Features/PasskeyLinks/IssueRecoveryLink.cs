using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.PasskeyLinks;

/// <summary>Issues a recovery link for an existing user. Revokes the user's previous recovery link.</summary>
public static class IssueRecoveryLink
{
    public static RouteHandlerBuilder MapIssueRecoveryLink(this IEndpointRouteBuilder links) => links.MapPost("/recovery", HandleAsync);

    private static async Task<Results<Created<IssuedPasskeyLinkResponse>, NotFound>> HandleAsync(
        IssueRecoveryLinkRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var (link, token) = PasskeyLink.CreateRecovery(request.UserId, PasskeyLinkIssuing.CurrentUserId(httpContext.User), time.GetUtcNow());
        await PasskeyLinkIssuing.ReplaceAsync(db, l => l.UserId == request.UserId, link, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created($"/api/passkey-links/{link.Id}", IssuedPasskeyLinkResponse.From(link, token));
    }
}

public sealed record IssueRecoveryLinkRequest(Guid UserId);
