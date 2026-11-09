using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>
/// Finishes a passkey sign-in: verifies the assertion and starts a persistent session.
/// Mirrors <see cref="SignInManager{TUser}.PasskeySignInAsync"/>, which only signs in for the browser session
/// and does not return the user.
/// </summary>
public static class SignInWithPasskey
{
    public const string AuthenticationMethod = "passkey";

    public static RouteHandlerBuilder MapSignInWithPasskey(this IEndpointRouteBuilder auth) =>
        auth.MapPost("/sign-in", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> HandleAsync(
        SignInWithPasskeyRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILoggerFactory loggerFactory)
    {
        if (request.Credential.ValueKind != JsonValueKind.Object)
        {
            return SignInFailed();
        }

        PasskeyAssertionResult<User> assertion;
        try
        {
            assertion = await signInManager.PerformPasskeyAssertionAsync(request.Credential.GetRawText());
        }
        catch (InvalidOperationException)
        {
            return SignInFailed();
        }

        var logger = loggerFactory.CreateLogger(typeof(SignInWithPasskey));
        if (!assertion.Succeeded)
        {
            logger.LogWarning("Passkey sign-in failed: {Reason}", assertion.Failure.Message);
            return SignInFailed();
        }

        var user = assertion.User;
        if (!await signInManager.CanSignInAsync(user) || await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Passkey sign-in refused for user {UserId}", user.Id);
            return SignInFailed();
        }

        if (!(await userManager.AddOrUpdatePasskeyAsync(user, assertion.Passkey)).Succeeded)
        {
            return SignInFailed();
        }

        await signInManager.SignInAsync(user, isPersistent: true, AuthenticationMethod);
        httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        ApiAntiforgery.IssueRequestToken(httpContext);

        return TypedResults.Ok(CurrentUserResponse.From(user));
    }

    private static ProblemHttpResult SignInFailed() =>
        TypedResults.Problem(title: "Passkey sign-in failed.", statusCode: StatusCodes.Status401Unauthorized);
}

public sealed record SignInWithPasskeyRequest(JsonElement Credential);
