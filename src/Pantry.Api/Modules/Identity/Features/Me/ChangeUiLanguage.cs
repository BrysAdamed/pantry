using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Me;

/// <summary>Saves the signed-in user's UI language, so every device they sign in on uses it.</summary>
public static class ChangeUiLanguage
{
    public static RouteHandlerBuilder MapChangeUiLanguage(this IEndpointRouteBuilder me) => me.MapPut("/ui-language", HandleAsync);

    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> HandleAsync(
        ChangeUiLanguageRequest request,
        HttpContext httpContext,
        UserManager<User> userManager,
        IdentityModuleDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.UiLanguage is not { } language)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.UiLanguage)] = ["UI language must be pl or en."],
            });
        }

        return await db.UpdatePreferenceAsync(userManager, httpContext.User, user => user.UiLanguage, language, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Unauthorized();
    }
}

public sealed record ChangeUiLanguageRequest(UiLanguage? UiLanguage);
