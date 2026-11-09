using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Me;

internal static class SignInMethods
{
    public static IReadOnlyList<SignInMethod> From(bool hasPasskey, bool hasPassword)
    {
        var methods = new List<SignInMethod>(2);
        if (hasPasskey)
        {
            methods.Add(SignInMethod.Passkey);
        }

        if (hasPassword)
        {
            methods.Add(SignInMethod.Password);
        }

        return methods;
    }

    public static async Task<IReadOnlyList<SignInMethod>> OfAsync(User user, IdentityModuleDbContext db, CancellationToken cancellationToken) =>
        From(await db.UserPasskeys.AnyAsync(p => p.UserId == user.Id, cancellationToken), user.HasPassword);

    /// <summary>Removing a passkey or the password is refused when it is the user's last way to sign in.</summary>
    public static ProblemHttpResult LastOne(string detail) => TypedResults.Problem(
        title: "The last sign-in method cannot be removed.",
        detail: detail,
        statusCode: StatusCodes.Status409Conflict);
}
