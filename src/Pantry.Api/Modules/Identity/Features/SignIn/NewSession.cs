using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.SignIn;

/// <summary>The end of every sign-in, first run and redeemed link: a persistent session and a token bound to the user.</summary>
internal static class NewSession
{
    public static async Task<Ok<CurrentUserResponse>> StartAsync(
        User user,
        HttpContext httpContext,
        SignInManager<User> signInManager,
        IdentityModuleDbContext db,
        CancellationToken cancellationToken,
        string? authenticationMethod = null)
    {
        await signInManager.SignInAsync(user, isPersistent: true, authenticationMethod);
        return await RespondAsync(user, httpContext, signInManager, db, cancellationToken);
    }

    /// <summary>For a session <see cref="SignInManager{TUser}"/> already started: switches the request to the user and answers with them.</summary>
    public static async Task<Ok<CurrentUserResponse>> RespondAsync(
        User user, HttpContext httpContext, SignInManager<User> signInManager, IdentityModuleDbContext db, CancellationToken cancellationToken)
    {
        httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        ApiAntiforgery.IssueRequestToken(httpContext);
        return TypedResults.Ok(await CurrentUserResponse.FromAsync(user, db, cancellationToken));
    }
}
