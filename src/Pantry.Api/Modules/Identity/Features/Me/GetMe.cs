using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Me;

public static class GetMe
{
    public static RouteHandlerBuilder MapGetMe(this IEndpointRouteBuilder me) => me.MapGet("", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> HandleAsync(
        HttpContext httpContext, UserManager<User> userManager, IdentityModuleDbContext db, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await CurrentUserResponse.FromAsync(user, db, cancellationToken));
    }
}

/// <summary>The signed-in user. <c>UserName</c> is null until the user chooses one together with a password.</summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string? UserName,
    UserRole Role,
    UiLanguage? UiLanguage,
    ThemePreference Theme,
    IReadOnlyList<SignInMethod> SignInMethods)
{
    internal static async Task<CurrentUserResponse> FromAsync(User user, IdentityModuleDbContext db, CancellationToken cancellationToken) => new(
        user.Id,
        user.DisplayName,
        user.ChosenUserName,
        user.Role,
        user.UiLanguage,
        user.Theme,
        await Me.SignInMethods.OfAsync(user, db, cancellationToken));
}
