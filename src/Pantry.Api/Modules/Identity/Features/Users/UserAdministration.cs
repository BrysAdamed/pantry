using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Users;

internal static class UserAdministration
{
    /// <summary>Whether <paramref name="user"/> is the only admin. Call under <see cref="IdentityLocks.LockUserAdministrationAsync"/>.</summary>
    public static async Task<bool> IsLastAdminAsync(IdentityModuleDbContext db, User user, CancellationToken cancellationToken) =>
        user.Role == UserRole.Admin && await db.Users.CountAsync(u => u.Role == UserRole.Admin, cancellationToken) == 1;

    public static ProblemHttpResult LastAdmin() => TypedResults.Problem(
        title: "The last admin cannot be removed or demoted.",
        detail: "Make another user an admin first.",
        statusCode: StatusCodes.Status409Conflict);

    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Changing the user failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
