using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Me;

public static class GetMe
{
    public static RouteHandlerBuilder MapGetMe(this IEndpointRouteBuilder api) => api.MapGet("/me", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext, UserManager<User> userManager)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(CurrentUserResponse.From(user));
    }
}

public sealed record CurrentUserResponse(Guid Id, string DisplayName, UserRole Role, UiLanguage? UiLanguage, ThemePreference Theme)
{
    public static CurrentUserResponse From(User user) => new(user.Id, user.DisplayName, user.Role, user.UiLanguage, user.Theme);
}
