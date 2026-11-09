using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>Ends the session on this device. Anonymous and idempotent, so an expired session can still be cleared.</summary>
public static class SignOut
{
    public static RouteHandlerBuilder MapSignOut(this IEndpointRouteBuilder auth) => auth.MapPost("/sign-out", HandleAsync);

    private static async Task<NoContent> HandleAsync(HttpContext httpContext, SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        ApiAntiforgery.IssueRequestToken(httpContext);
        return TypedResults.NoContent();
    }
}
