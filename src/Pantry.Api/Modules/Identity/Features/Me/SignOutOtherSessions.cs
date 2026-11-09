using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Me;

/// <summary>
/// Ends the signed-in user's sessions on every other device: a new security stamp fails their next session check
/// (ADR 0017), and this session is reissued with the new stamp.
/// </summary>
public static class SignOutOtherSessions
{
    public static RouteHandlerBuilder MapSignOutOtherSessions(this IEndpointRouteBuilder me) =>
        me.MapPost("/sign-out-others", HandleAsync);

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext, UserManager<User> userManager, SignInManager<User> signInManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        await OtherSessions.RevokeAsync(user, userManager);
        await signInManager.RefreshSignInAsync(user);
        return TypedResults.NoContent();
    }
}

internal static class OtherSessions
{
    /// <summary>
    /// Gives the user a new security stamp, so every session fails its next check. Reissue the current session with
    /// <see cref="SignInManager{TUser}.RefreshSignInAsync"/> afterwards to keep it.
    /// </summary>
    public static async Task RevokeAsync(User user, UserManager<User> userManager)
    {
        var result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Updating the security stamp failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
