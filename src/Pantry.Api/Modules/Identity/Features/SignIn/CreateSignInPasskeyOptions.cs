using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>Starts a passkey sign-in: WebAuthn request options without a user, so the browser offers every discoverable passkey.</summary>
public static class CreateSignInPasskeyOptions
{
    public static RouteHandlerBuilder MapCreateSignInPasskeyOptions(this IEndpointRouteBuilder auth) =>
        auth.MapPost("/passkey-options", HandleAsync);

    private static async Task<ContentHttpResult> HandleAsync(SignInManager<User> signInManager) =>
        TypedResults.Content(await signInManager.MakePasskeyRequestOptionsAsync(user: null), "application/json");
}
