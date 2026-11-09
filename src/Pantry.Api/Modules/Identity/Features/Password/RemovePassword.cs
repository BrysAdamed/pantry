using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Password;

/// <summary>
/// Removes the signed-in user's password and signs out their other devices. Only for a user with a passkey: the last
/// sign-in method stays. The username stays too.
/// </summary>
public static class RemovePassword
{
    public static RouteHandlerBuilder MapRemovePassword(this IEndpointRouteBuilder password) => password.MapDelete("", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        CancellationToken cancellationToken)
    {
        if (userManager.GetUserId(httpContext.User) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await MyPassword.FindLockedAsync(userId, db, userManager, cancellationToken) is not { } user)
        {
            return TypedResults.Unauthorized();
        }

        if (!user.HasPassword)
        {
            return MyPassword.NoPassword();
        }

        if (!await db.UserPasskeys.AnyAsync(p => p.UserId == user.Id, cancellationToken))
        {
            return SignInMethods.LastOne("Add a passkey first.");
        }

        NewCredentials.EnsureSucceeded(await userManager.RemovePasswordAsync(user));
        return await MyPassword.FinishAsync(user, transaction, db, userManager, signInManager, cancellationToken);
    }
}
