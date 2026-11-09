using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>The signed-in user's passkeys, oldest first.</summary>
public static class GetMyPasskeys
{
    public static RouteHandlerBuilder MapGetMyPasskeys(this IEndpointRouteBuilder passkeys) => passkeys.MapGet("", HandleAsync);

    private static async Task<Results<Ok<List<PasskeyResponse>>, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext, UserManager<User> userManager, IdentityModuleDbContext db, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userManager.GetUserId(httpContext.User), out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var passkeys = await db.UserPasskeys
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.CredentialId,
                p.Data.Name,
                p.Data.CreatedAt,
                LastUsedAt = EF.Property<DateTimeOffset?>(p, PasskeyUsage.LastUsedAt),
            })
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(passkeys
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PasskeyResponse(MyPasskeys.ToId(p.CredentialId), p.Name, p.CreatedAt, p.LastUsedAt))
            .ToList());
    }
}
