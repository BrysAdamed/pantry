using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Users;

/// <summary>
/// Removes a user with their passkeys and recovery links. The member stays, without an account, and can be invited again.
/// The user's sessions end at the next session check, which no longer finds the user.
/// </summary>
public static class RemoveUser
{
    public static RouteHandlerBuilder MapRemoveUser(this IEndpointRouteBuilder users) => users.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        IHouseholdModule household,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (await UserAdministration.IsLastAdminAsync(db, user, cancellationToken))
        {
            return UserAdministration.LastAdmin();
        }

        await household.UnlinkUserAsync(user.Id, transaction.GetDbTransaction(), cancellationToken);
        UserAdministration.EnsureSucceeded(await userManager.DeleteAsync(user));
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
