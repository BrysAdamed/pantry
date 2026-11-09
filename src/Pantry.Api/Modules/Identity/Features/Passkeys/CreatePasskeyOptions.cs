using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>Starts adding a passkey to the signed-in user: WebAuthn creation options that exclude their existing passkeys.</summary>
public static class CreatePasskeyOptions
{
    public static RouteHandlerBuilder MapCreatePasskeyOptions(this IEndpointRouteBuilder passkeys) =>
        passkeys.MapPost("/options", HandleAsync);

    private static async Task<Results<ContentHttpResult, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext, UserManager<User> userManager, SignInManager<User> signInManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var userEntity = new PasskeyUserEntity { Id = user.Id.ToString(), Name = user.DisplayName, DisplayName = user.DisplayName };
        return TypedResults.Content(await signInManager.MakePasskeyCreationOptionsAsync(userEntity), "application/json");
    }
}
