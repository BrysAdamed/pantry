using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Features.SignIn;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Redeem;

/// <summary>
/// Finishes redeeming a link with a passkey and signs the person in. An invite creates the user and links it to the member
/// in one transaction (ADR 0015); a recovery link adds the new passkey to the existing user and ends their other sessions.
/// Either way the link is used up.
/// </summary>
public static class CompleteRedeem
{
    public static RouteHandlerBuilder MapCompleteRedeem(this IEndpointRouteBuilder redeem) => redeem.MapPost("/complete", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> HandleAsync(
        string token,
        CompleteRedeemRequest request,
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
            return AttestationFailed("No passkey creation is in progress. Open the link again.");
        }

        if (!attestation.Succeeded)
        {
            loggerFactory.CreateLogger(typeof(CompleteRedeem)).LogWarning("Link passkey attestation failed: {Reason}", attestation.Failure.Message);
            return AttestationFailed("The passkey could not be verified.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var now = time.GetUtcNow();
        var (redeemable, problem) = await RedeemableLinks.FindAsync(token, db, household, now, cancellationToken);
        if (redeemable is null)
        {
            return problem!;
        }

        User user;
        if (redeemable.User is { } existing)
        {
            if (attestation.UserEntity.Id != existing.Id.ToString())
            {
                return AttestationFailed("The passkey was created for someone else.");
            }

            user = existing;
            user.UiLanguage ??= request.UiLanguage;
            RedeemableLinks.EnsureSucceeded(await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey));
            await RedeemableLinks.RecoverAsync(user, userManager);
        }
        else
        {
            if (!Guid.TryParse(attestation.UserEntity.Id, out var userId) || await userManager.FindByIdAsync(userId.ToString()) is not null)
            {
                return AttestationFailed("The passkey was created for someone else.");
            }

            user = User.Create(userId, attestation.UserEntity.DisplayName, UserRole.User);
            user.UiLanguage = request.UiLanguage;
            RedeemableLinks.EnsureSucceeded(await userManager.CreateAsync(user));
            RedeemableLinks.EnsureSucceeded(await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey));
            if (!await household.LinkMemberToUserAsync(redeemable.Link.MemberId!.Value, user.Id, transaction.GetDbTransaction(), cancellationToken))
            {
                return RedeemableLinks.Gone(PasskeyLinkState.Revoked);
            }
        }

        redeemable.Link.MarkUsed(now);
        await db.SaveChangesAsync(cancellationToken);
        await db.RecordPasskeyUseAsync(attestation.Passkey.CredentialId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await NewSession.StartAsync(user, httpContext, signInManager, db, cancellationToken);
    }

    private static ProblemHttpResult AttestationFailed(string detail) =>
        TypedResults.Problem(title: "Passkey creation failed.", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

/// <summary>The new passkey, and the UI language of the redeem screen, kept as the user's UI language unless they have one.</summary>
public sealed record CompleteRedeemRequest(JsonElement Credential, UiLanguage? UiLanguage = null);
