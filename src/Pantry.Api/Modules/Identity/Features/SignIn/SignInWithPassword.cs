using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Auth;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>
/// Signs in with a username and password and starts a persistent session. An unknown username, a user without a password,
/// a wrong password and a locked-out user all get the same answer after one password hash, so neither the answer nor its
/// time tells which usernames exist. Attempts on one user are serialized, so parallel guesses all count towards the
/// lockout (ADR 0018).
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
        if (password.Length > PasswordConfiguration.MaxLength)
        {
            return SignInFailed();
        }

        var userName = UserNames.Clean(request.UserName);
        var user = userName.Length == 0 ? null : await userManager.FindByNameAsync(userName);
        if (user is not { HasPassword: true })
        {
            return Refuse(userManager, password);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserPasswordChecksAsync(user.Id, cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        if (db.Entry(user).State == EntityState.Detached || !user.HasPassword || await userManager.IsLockedOutAsync(user))
        {
            return Refuse(userManager, password);
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        await transaction.CommitAsync(cancellationToken);
        if (!result.Succeeded)
        {
            loggerFactory.CreateLogger(typeof(SignInWithPassword))
                .LogWarning("Password sign-in refused for user {UserId}, now locked out: {LockedOut}", user.Id, result.IsLockedOut);
            return SignInFailed();
        }

        return await NewSession.RespondAsync(user, httpContext, signInManager, db, cancellationToken);
    }

    /// <summary>Hashes the password anyway, so a refusal takes as long as checking a wrong password.</summary>
    private static ProblemHttpResult Refuse(UserManager<User> userManager, string password)
    {
        userManager.PasswordHasher.HashPassword(NoUser, password);
        return SignInFailed();
    }

    private static ProblemHttpResult SignInFailed() =>
        TypedResults.Problem(title: "Password sign-in failed.", statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record SignInWithPasswordRequest(string? UserName, string? Password);
