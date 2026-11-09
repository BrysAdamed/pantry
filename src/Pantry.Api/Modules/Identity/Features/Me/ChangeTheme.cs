using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Me;

/// <summary>Saves the signed-in user's theme, so every device they sign in on uses it.</summary>
public static class ChangeTheme
{
    public static RouteHandlerBuilder MapChangeTheme(this IEndpointRouteBuilder me) => me.MapPut("/theme", HandleAsync);

    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> HandleAsync(
        ChangeThemeRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        IdentityModuleDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.Theme is not { } theme)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Theme)] = ["Theme must be system, light or dark."],
            });
        }

        return await db.UpdatePreferenceAsync(userManager, httpContext.User, user => user.Theme, theme, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Unauthorized();
    }
}

public sealed record ChangeThemeRequest(ThemePreference? Theme);
