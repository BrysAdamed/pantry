using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.PasskeyLinks;

/// <summary>Revokes an invite or recovery link. Revoking a revoked or expired link again is a no-op; a used link cannot be revoked.</summary>
public static class RevokePasskeyLink
{
    public static RouteHandlerBuilder MapRevokePasskeyLink(this IEndpointRouteBuilder links) => links.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id, IdentityModuleDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var link = await db.PasskeyLinks.FindAsync([id], cancellationToken);
        if (link is null)
        {
            return TypedResults.NotFound();
        }

        var now = time.GetUtcNow();
        if (link.StateAt(now) == PasskeyLinkState.Used)
        {
            return TypedResults.Problem(title: "The link was already used.", statusCode: StatusCodes.Status409Conflict);
        }

        link.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
