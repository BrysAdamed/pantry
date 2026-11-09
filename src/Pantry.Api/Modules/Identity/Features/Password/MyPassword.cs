using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Password;

/// <summary>
/// What setting, changing and removing the signed-in user's password share. Each runs under the user administration lock,
/// like removing a passkey, so "the last sign-in method stays" and "a username is unique" hold under concurrent requests.
/// </summary>
internal static class MyPassword
{
    /// <summary>
    /// The signed-in user, read again after taking the locks: the session check earlier in the request may have loaded
    /// the user into the context before a concurrent change committed. The password-check lock also serializes with
    /// password sign-ins of the same user, so failed attempts from both count.
    /// </summary>
    public static async Task<User?> FindLockedAsync(
        string userId, IdentityModuleDbContext db, UserManager<User> userManager, CancellationToken cancellationToken)
    {
        await db.LockUserAdministrationAsync(cancellationToken);
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        await db.LockUserPasswordChecksAsync(user.Id, cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        return db.Entry(user).State == EntityState.Detached ? null : user;
    }

    /// <summary>
    /// Ends the sessions on other devices, commits, and keeps this session with the new security stamp; answers with the
    /// changed user.
    /// </summary>
    public static async Task<Ok<CurrentUserResponse>> FinishAsync(
        User user,
        IDbContextTransaction transaction,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        CancellationToken cancellationToken)
    {
        await OtherSessions.RevokeAsync(user, userManager);
        await transaction.CommitAsync(cancellationToken);
        await signInManager.RefreshSignInAsync(user);
        return TypedResults.Ok(await CurrentUserResponse.FromAsync(user, db, cancellationToken));
    }

    public static ProblemHttpResult NoPassword() =>
        TypedResults.Problem(title: "No password is set.", statusCode: StatusCodes.Status409Conflict);
}
