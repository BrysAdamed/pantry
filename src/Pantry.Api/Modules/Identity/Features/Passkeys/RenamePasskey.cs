using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>Renames one of the signed-in user's passkeys, so they can tell their devices apart.</summary>
public static class RenamePasskey
{
    public static RouteHandlerBuilder MapRenamePasskey(this IEndpointRouteBuilder passkeys) => passkeys.MapPut("/{id}/name", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ValidationProblem, UnauthorizedHttpResult>> HandleAsync(
        string id,
        RenamePasskeyRequest request,
        HttpContext httpContext,
        UserManager<User> userManager)
    {
        var name = MyPasskeys.NormalizeName(request.Name);
        if (name is null || !MyPasskeys.IsValidName(name))
        {
            return MyPasskeys.InvalidName();
        }

        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (MyPasskeys.FromId(id) is not { } credentialId || await userManager.GetPasskeyAsync(user, credentialId) is not { } passkey)
        {
            return TypedResults.NotFound();
        }

        passkey.Name = name;
        var result = await userManager.AddOrUpdatePasskeyAsync(user, passkey);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Renaming the passkey failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return TypedResults.NoContent();
    }
}

public sealed record RenamePasskeyRequest(string? Name);
