using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Password;

/// <summary>Changes the signed-in user's password, given the current one, and signs out their other devices.</summary>
public static class ChangePassword
{
    private const string CurrentPasswordField = "CurrentPassword";
    private const string NewPasswordField = "NewPassword";

    public static RouteHandlerBuilder MapChangePassword(this IEndpointRouteBuilder password) => password.MapPut("", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        ChangePasswordRequest request,
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

        if (await NewCredentials.ValidateAsync(userManager, user, userName: null, request.NewPassword, NewPasswordField) is { } invalid)
        {
            return invalid;
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword ?? string.Empty, request.NewPassword!);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [CurrentPasswordField] = ["The current password is wrong."],
            });
        }

        NewCredentials.EnsureSucceeded(result);
        return await MyPassword.FinishAsync(user, transaction, db, userManager, signInManager, cancellationToken);
    }
}

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
