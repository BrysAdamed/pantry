using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Setup;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Redeem;

/// <summary>
/// Starts redeeming a link: WebAuthn creation options for a new user (invite, with the chosen display name)
/// or for the existing user (recovery, the display name is ignored).
/// </summary>
public static class CreateRedeemPasskeyOptions
{
    public static RouteHandlerBuilder MapCreateRedeemPasskeyOptions(this IEndpointRouteBuilder redeem) =>
        redeem.MapPost("/passkey-options", HandleAsync);

    private static async Task<Results<ContentHttpResult, ValidationProblem, ProblemHttpResult>> HandleAsync(
        string token,
        RedeemPasskeyOptionsRequest request,
        IdentityModuleDbContext db,
        IHouseholdModule household,
        SignInManager<User> signInManager,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var (redeemable, problem) = await RedeemableLinks.FindAsync(token, db, household, time.GetUtcNow(), cancellationToken);
        if (redeemable is null)
        {
            return problem!;
        }

        PasskeyUserEntity userEntity;
        if (redeemable.User is { } user)
        {
            userEntity = new PasskeyUserEntity { Id = user.Id.ToString(), Name = user.DisplayName, DisplayName = user.DisplayName };
        }
        else
        {
            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (!User.IsValidDisplayName(displayName))
            {
                return DisplayNameValidation.Problem();
            }

            userEntity = new PasskeyUserEntity { Id = Guid.CreateVersion7().ToString(), Name = displayName, DisplayName = displayName };
        }

        return TypedResults.Content(await signInManager.MakePasskeyCreationOptionsAsync(userEntity), "application/json");
    }
}

public sealed record RedeemPasskeyOptionsRequest(string? DisplayName);
