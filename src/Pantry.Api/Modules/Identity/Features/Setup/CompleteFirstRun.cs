using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Infrastructure.Antiforgery;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>Finishes the first run: verifies the new passkey, creates the admin and signs them in.</summary>
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
        await db.LockFirstRunAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return FirstRunProblems.AlreadyDone();
        }

        var admin = User.CreateAdmin(Guid.Parse(attestation.UserEntity.Id), attestation.UserEntity.DisplayName);
        EnsureSucceeded(await userManager.CreateAsync(admin));
        EnsureSucceeded(await userManager.AddOrUpdatePasskeyAsync(admin, attestation.Passkey));
        await household.CompleteFirstRunAsync(new FirstRunAdmin(admin.Id, admin.DisplayName), transaction.GetDbTransaction(), cancellationToken);
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
