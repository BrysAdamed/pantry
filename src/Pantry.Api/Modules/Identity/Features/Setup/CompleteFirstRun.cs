using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>Finishes the first run: verifies the new passkey, creates the admin and signs them in.</summary>
public static class CompleteFirstRun
{
    private const long FirstRunLockKey = 0x5041_4E54_5259_0001;

    public static RouteHandlerBuilder MapCompleteFirstRun(this IEndpointRouteBuilder setup) =>
        setup.MapPost("/complete", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> HandleAsync(
        CompleteFirstRunRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({FirstRunLockKey})", cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return FirstRunProblems.AlreadyDone();
        }

        var admin = User.CreateAdmin(Guid.Parse(attestation.UserEntity.Id), attestation.UserEntity.DisplayName);
        EnsureSucceeded(await userManager.CreateAsync(admin));
        EnsureSucceeded(await userManager.AddOrUpdatePasskeyAsync(admin, attestation.Passkey));
        // TODO PAN-21: create the admin's household member and the default meal slots through IHouseholdModule here.
        await transaction.CommitAsync(cancellationToken);

        await signInManager.SignInAsync(admin, isPersistent: true);
        httpContext.User = await signInManager.CreateUserPrincipalAsync(admin);
        ApiAntiforgery.IssueRequestToken(httpContext);

        return TypedResults.Ok(CurrentUserResponse.From(admin));
    }

    private static ProblemHttpResult AttestationFailed(string detail) =>
        TypedResults.Problem(title: "Passkey creation failed.", detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Creating the admin failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}

public sealed record CompleteFirstRunRequest(JsonElement Credential);
