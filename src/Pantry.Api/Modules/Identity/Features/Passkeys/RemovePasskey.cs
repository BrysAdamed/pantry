using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>
/// Removes one of the signed-in user's passkeys, for example of a lost phone, and ends their sessions on other devices,
/// because a session does not know which passkey started it. The last passkey of a user without a password cannot be removed.
/// </summary>
public static class RemovePasskey
{
    public static RouteHandlerBuilder MapRemovePasskey(this IEndpointRouteBuilder passkeys) => passkeys.MapDelete("/{id}", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult, UnauthorizedHttpResult>> HandleAsync(
        string id,
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

        if (MyPasskeys.FromId(id) is not { } credentialId)
        {
            return TypedResults.NotFound();
        }

        User? user;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.LockUserAdministrationAsync(cancellationToken);
            user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return TypedResults.Unauthorized();
            }

            await db.Entry(user).ReloadAsync(cancellationToken);
            var passkeys = await db.UserPasskeys.Where(p => p.UserId == user.Id).Select(p => p.CredentialId).ToListAsync(cancellationToken);
            if (!passkeys.Any(p => p.AsSpan().SequenceEqual(credentialId)))
            {
                return TypedResults.NotFound();
            }

            if (passkeys.Count == 1 && !user.HasPassword)
            {
                return SignInMethods.LastOne("Add another passkey or set a password first.");
            }

            var result = await userManager.RemovePasskeyAsync(user, credentialId);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Removing the passkey failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }

            await OtherSessions.RevokeAsync(user, userManager);
            await transaction.CommitAsync(cancellationToken);
        }

        await signInManager.RefreshSignInAsync(user);
        return TypedResults.NoContent();
    }
}
