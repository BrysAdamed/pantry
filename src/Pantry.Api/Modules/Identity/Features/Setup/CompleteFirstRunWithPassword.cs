using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Features.Password;
using Pantry.Api.Modules.Identity.Features.SignIn;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>Finishes the first run with a username and password instead of a passkey: creates the admin and signs them in.</summary>
public static class CompleteFirstRunWithPassword
{
    public static RouteHandlerBuilder MapCompleteFirstRunWithPassword(this IEndpointRouteBuilder setup) =>
        setup.MapPost("/complete-with-password", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        CompleteFirstRunWithPasswordRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IHouseholdModule household,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (!User.IsValidDisplayName(displayName))
        {
            return DisplayNameValidation.Problem();
        }

        var userName = UserNames.Clean(request.UserName);
        var admin = User.CreateAdmin(Guid.CreateVersion7(), displayName, userName);
        admin.UiLanguage = request.UiLanguage;
        if (await NewCredentials.ValidateAsync(userManager, admin, userName, request.Password) is { } invalid)
        {
            return invalid;
        }

        var problem = await FirstRunSetup.CreateAdminAsync(
            admin,
            async () => FirstRunSetup.EnsureSucceeded(await userManager.AddPasswordAsync(admin, request.Password!)),
            db,
            userManager,
            household,
            cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        return await NewSession.StartAsync(admin, httpContext, signInManager, db, cancellationToken);
    }
}

/// <summary>The admin's display name, username and password, and the UI language the app was set up in (see <see cref="CompleteFirstRunRequest"/>).</summary>
public sealed record CompleteFirstRunWithPasswordRequest(string? DisplayName, string? UserName, string? Password, UiLanguage? UiLanguage = null);
