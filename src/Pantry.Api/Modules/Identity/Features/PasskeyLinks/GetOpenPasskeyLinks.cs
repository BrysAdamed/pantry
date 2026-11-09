using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.PasskeyLinks;

/// <summary>Invites and recovery links that can still be used. Tokens are stored hashed, so they are not part of the answer.</summary>
public static class GetOpenPasskeyLinks
{
    public static RouteHandlerBuilder MapGetOpenPasskeyLinks(this IEndpointRouteBuilder links) => links.MapGet("/", HandleAsync);

    private static async Task<IReadOnlyList<OpenPasskeyLinkResponse>> HandleAsync(
        IdentityModuleDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return await db.PasskeyLinks.AsNoTracking()
            .Where(l => l.UsedAt == null && l.RevokedAt == null && l.ExpiresAt > now)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new OpenPasskeyLinkResponse(l.Id, l.Kind, l.MemberId, l.UserId, l.CreatedAt, l.ExpiresAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record OpenPasskeyLinkResponse(Guid Id, PasskeyLinkKind Kind, Guid? MemberId, Guid? UserId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
