using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.PasskeyLinks;

/// <summary>A link right after it was issued. The only response that carries the token.</summary>
public sealed record IssuedPasskeyLinkResponse(Guid Id, PasskeyLinkKind Kind, string Token, Guid? MemberId, Guid? UserId, DateTimeOffset ExpiresAt)
{
    public static IssuedPasskeyLinkResponse From(PasskeyLink link, string token) =>
        new(link.Id, link.Kind, token, link.MemberId, link.UserId, link.ExpiresAt);
}

internal static class PasskeyLinkIssuing
{
    /// <summary>Revokes the pending links matching <paramref name="target"/> and stores the new link, in the caller's transaction.</summary>
    public static async Task ReplaceAsync(
        IdentityModuleDbContext db,
        Expression<Func<PasskeyLink, bool>> target,
        PasskeyLink link,
        CancellationToken cancellationToken)
    {
        var pending = await db.PasskeyLinks.Where(target).Where(l => l.UsedAt == null && l.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var previous in pending)
        {
            previous.Revoke(link.CreatedAt);
        }

        await db.SaveChangesAsync(cancellationToken);
        db.PasskeyLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static Guid CurrentUserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The signed-in user has no id claim."));
}
