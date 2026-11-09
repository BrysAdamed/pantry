using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Password;

/// <summary>
/// Gives the signed-in user a password, with a username if they have none yet, and signs out their other devices.
/// A user who already has a password changes it instead (409).
/// </summary>
public static class SetPassword
{
    public static RouteHandlerBuilder MapSetPassword(this IEndpointRouteBuilder password) => password.MapPost("", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        SetPasswordRequest request,
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

        if (user.HasPassword)
        {
            return TypedResults.Problem(title: "A password is already set.", detail: "Change it instead.", statusCode: StatusCodes.Status409Conflict);
        }

        var newUserName = user.ChosenUserName is null ? UserNames.Clean(request.UserName) : null;
        if (await NewCredentials.ValidateAsync(userManager, user, newUserName, request.Password) is { } invalid)
        {
            return invalid;
        }

        if (newUserName is not null)
        {
            if (await NewCredentials.EnsureUserNameFreeAsync(userManager, newUserName) is { } taken)
            {
                return taken;
            }

            NewCredentials.EnsureSucceeded(await userManager.SetUserNameAsync(user, newUserName));
        }

        NewCredentials.EnsureSucceeded(await userManager.AddPasswordAsync(user, request.Password!));
        return await MyPassword.FinishAsync(user, transaction, db, userManager, signInManager, cancellationToken);
    }
}

/// <summary><c>UserName</c> is needed when the user has none yet and ignored otherwise: a username is chosen once.</summary>
public sealed record SetPasswordRequest(string? UserName, string? Password);
