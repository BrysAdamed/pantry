using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>
/// Signs in with a username and password and starts a persistent session. An unknown username, a user without a password,
/// a wrong password and a locked-out user all get the same answer, so it does not tell which usernames exist. Failed
/// attempts count per user; Identity locks the user out after too many (ADR 0018).
/// </summary>
public static class SignInWithPassword
{
    private static readonly User NoUser = User.Create(Guid.Empty, "-", UserRole.User);

    public static RouteHandlerBuilder MapSignInWithPassword(this IEndpointRouteBuilder auth) =>
        auth.MapPost("/sign-in/password", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> HandleAsync(
        SignInWithPasswordRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IdentityModuleDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var password = request.Password ?? string.Empty;
        var userName = UserNames.Clean(request.UserName);
        var user = userName.Length == 0 ? null : await userManager.FindByNameAsync(userName);
        if (user is not { HasPassword: true })
        {
            TakeAsLongAsAPasswordCheck(userManager, password);
            return SignInFailed();
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var logger = loggerFactory.CreateLogger(typeof(SignInWithPassword));
            logger.LogWarning("Password sign-in refused for user {UserId}, locked out: {LockedOut}", user.Id, result.IsLockedOut);
            return SignInFailed();
        }

        return await NewSession.RespondAsync(user, httpContext, signInManager, db, cancellationToken);
    }

    private static void TakeAsLongAsAPasswordCheck(UserManager<User> userManager, string password) =>
        userManager.PasswordHasher.HashPassword(NoUser, password);

    private static ProblemHttpResult SignInFailed() =>
        TypedResults.Problem(title: "Password sign-in failed.", statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record SignInWithPasswordRequest(string? UserName, string? Password);
