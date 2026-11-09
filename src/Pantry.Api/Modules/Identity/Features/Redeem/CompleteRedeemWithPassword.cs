using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Features.Me;
using Pantry.Api.Modules.Identity.Features.Password;
using Pantry.Api.Modules.Identity.Features.Setup;
using Pantry.Api.Modules.Identity.Features.SignIn;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Redeem;

/// <summary>
/// Finishes redeeming a link with a username and password instead of a passkey, and signs the person in. An invite creates
/// the user with the password and links the member; a recovery link sets a new password on the existing user (with a
/// username if they have none yet) and ends their other sessions. Either way the link is used up.
/// </summary>
public static class CompleteRedeemWithPassword
{
    public static RouteHandlerBuilder MapCompleteRedeemWithPassword(this IEndpointRouteBuilder redeem) =>
        redeem.MapPost("/complete-with-password", HandleAsync);

    private static async Task<Results<Ok<CurrentUserResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        string token,
        CompleteRedeemWithPasswordRequest request,
        HttpContext httpContext,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IHouseholdModule household,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockUserAdministrationAsync(cancellationToken);
        var now = time.GetUtcNow();
        var (redeemable, problem) = await RedeemableLinks.FindAsync(token, db, household, now, cancellationToken);
        if (redeemable is null)
        {
            return problem!;
        }

        var password = request.Password ?? string.Empty;
        User user;
        if (redeemable.User is { } existing)
        {
            user = existing;
            var newUserName = user.ChosenUserName is null ? UserNames.Clean(request.UserName) : null;
            if (await NewCredentials.ValidateAsync(userManager, user, newUserName, password) is { } invalid)
            {
                return invalid;
            }

            if (newUserName is not null)
            {
                if (await NewCredentials.EnsureUserNameFreeAsync(userManager, newUserName) is { } taken)
                {
                    return taken;
                }
            }

            await RedeemableLinks.RecoverAsync(user, db, userManager, cancellationToken);
            if (newUserName is not null)
            {
                RedeemableLinks.EnsureSucceeded(await userManager.SetUserNameAsync(user, newUserName));
            }

            if (user.HasPassword)
            {
                RedeemableLinks.EnsureSucceeded(await userManager.RemovePasswordAsync(user));
            }

            RedeemableLinks.EnsureSucceeded(await userManager.AddPasswordAsync(user, password));
            user.UiLanguage ??= request.UiLanguage;
        }
        else
        {
            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (!User.IsValidDisplayName(displayName))
            {
                return DisplayNameValidation.Problem();
            }

            var userName = UserNames.Clean(request.UserName);
            user = User.Create(Guid.CreateVersion7(), displayName, UserRole.User, userName);
            user.UiLanguage = request.UiLanguage;
            if (await NewCredentials.ValidateAsync(userManager, user, userName, password) is { } invalid)
            {
                return invalid;
            }

            if (await NewCredentials.EnsureUserNameFreeAsync(userManager, userName) is { } taken)
            {
                return taken;
            }

            RedeemableLinks.EnsureSucceeded(await userManager.CreateAsync(user, password));
            if (!await household.LinkMemberToUserAsync(redeemable.Link.MemberId!.Value, user.Id, transaction.GetDbTransaction(), cancellationToken))
            {
                return RedeemableLinks.Gone(PasskeyLinkState.Revoked);
            }
        }

        redeemable.Link.MarkUsed(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await NewSession.StartAsync(user, httpContext, signInManager, db, cancellationToken);
    }
}

/// <summary>
/// <c>DisplayName</c> is for an invite and ignored for recovery. <c>UserName</c> is needed for an invite and for a
/// recovering user without a username, and ignored otherwise. The UI language works as in <see cref="CompleteRedeemRequest"/>.
/// </summary>
public sealed record CompleteRedeemWithPasswordRequest(string? DisplayName, string? UserName, string? Password, UiLanguage? UiLanguage = null);
