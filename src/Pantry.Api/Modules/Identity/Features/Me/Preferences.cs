using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Me;

internal static class Preferences
{
    /// <summary>
    /// Writes one preference column of the signed-in user. A single-column update, so changing the language and the theme
    /// at the same time cannot fail on the user's concurrency stamp. False when the user no longer exists.
    /// </summary>
    public static async Task<bool> UpdatePreferenceAsync<T>(
        this IdentityModuleDbContext db,
        UserManager<User> userManager,
        ClaimsPrincipal principal,
        Expression<Func<User, T>> preference,
        T value,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userManager.GetUserId(principal), out var userId))
        {
            return false;
        }

        var updated = await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(preference, value), cancellationToken);
        return updated == 1;
    }
}
