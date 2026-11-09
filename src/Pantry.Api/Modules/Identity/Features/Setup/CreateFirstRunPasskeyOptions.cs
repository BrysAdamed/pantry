using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>Starts the first run: returns WebAuthn creation options for the future admin's passkey.</summary>
public static class CreateFirstRunPasskeyOptions
{
    public static RouteHandlerBuilder MapCreateFirstRunPasskeyOptions(this IEndpointRouteBuilder setup) =>
        setup.MapPost("/passkey-options", HandleAsync);

    private static async Task<Results<ContentHttpResult, ValidationProblem, ProblemHttpResult>> HandleAsync(
        FirstRunPasskeyOptionsRequest request,
        IdentityModuleDbContext db,
        SignInManager<User> signInManager,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (displayName.Length is 0 or > User.DisplayNameMaxLength)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] = [$"Display name must have 1 to {User.DisplayNameMaxLength} characters."],
            });
        }

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return FirstRunProblems.AlreadyDone();
        }

        var userEntity = new PasskeyUserEntity
        {
            Id = Guid.CreateVersion7().ToString(),
            Name = displayName,
            DisplayName = displayName,
        };
        var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(userEntity);
        return TypedResults.Content(optionsJson, "application/json");
    }
}

public sealed record FirstRunPasskeyOptionsRequest(string? DisplayName);

internal static class FirstRunProblems
{
    public static ProblemHttpResult AlreadyDone() =>
        TypedResults.Problem(title: "First run is already done.", statusCode: StatusCodes.Status409Conflict);
}
