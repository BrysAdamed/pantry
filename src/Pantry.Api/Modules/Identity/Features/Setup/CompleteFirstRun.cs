using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Features.SignIn;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>Finishes the first run with a passkey: verifies the new passkey, creates the admin and signs them in.</summary>
public static class CompleteFirstRun
{
    public static RouteHandlerBuilder MapCompleteFirstRun(this IEndpointRouteBuilder setup) =>
        setup.MapPost("/complete", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> HandleAsync(
        CompleteFirstRunRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IHouseholdModule household,
        TimeProvider time,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (request.Credential.ValueKind != JsonValueKind.Object)
        {
            return AttestationFailed("The passkey credential is missing.");
        }

        PasskeyAttestationResult attestation;
        try
        {
            attestation = await signInManager.PerformPasskeyAttestationAsync(request.Credential.GetRawText());
        }
        catch (InvalidOperationException)
        {
            return AttestationFailed("No passkey creation is in progress. Start the first run again.");
        }

        if (!attestation.Succeeded)
        {
            loggerFactory.CreateLogger(typeof(CompleteFirstRun)).LogWarning("First-run passkey attestation failed: {Reason}", attestation.Failure.Message);
            return AttestationFailed("The passkey could not be verified.");
        }

        var admin = User.CreateAdmin(Guid.Parse(attestation.UserEntity.Id), attestation.UserEntity.DisplayName);
        admin.UiLanguage = request.UiLanguage;
        var problem = await FirstRunSetup.CreateAdminAsync(
            admin,
            async () =>
            {
                FirstRunSetup.EnsureSucceeded(await userManager.AddOrUpdatePasskeyAsync(admin, attestation.Passkey));
                await db.RecordPasskeyUseAsync(attestation.Passkey.CredentialId, time.GetUtcNow(), cancellationToken);
            },
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

    private static ProblemHttpResult AttestationFailed(string detail) =>
        TypedResults.Problem(title: "Passkey creation failed.", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

/// <summary>
/// The new passkey, and the UI language the admin set the app up in: it becomes the admin's UI language and names the
/// default meal slots.
/// </summary>
public sealed record CompleteFirstRunRequest(JsonElement Credential, UiLanguage? UiLanguage = null);
