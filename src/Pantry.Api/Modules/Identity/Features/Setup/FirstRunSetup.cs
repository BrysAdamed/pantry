using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Identity.Domain;
using Pantry.Api.Modules.Identity.Persistence;

namespace Pantry.Api.Modules.Identity.Features.Setup;

/// <summary>The part of the first run both sign-in methods share.</summary>
internal static class FirstRunSetup
{
    /// <summary>
    /// Creates <paramref name="admin"/>, gives them their sign-in method and sets up the household, in one transaction
    /// serialized with other first runs. Answers 409 when an admin already exists.
    /// </summary>
    public static async Task<ProblemHttpResult?> CreateAdminAsync(
        User admin,
        Func<Task> addSignInMethod,
        IdentityModuleDbContext db,
        UserManager<User> userManager,
        IHouseholdModule household,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockFirstRunAsync(cancellationToken);
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return FirstRunProblems.AlreadyDone();
        }

        EnsureSucceeded(await userManager.CreateAsync(admin));
        await addSignInMethod();
        var firstRunAdmin = new FirstRunAdmin(admin.Id, admin.DisplayName, ToContentLanguage(admin.UiLanguage));
        await household.CompleteFirstRunAsync(firstRunAdmin, transaction.GetDbTransaction(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return null;
    }

    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Creating the admin failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private static ContentLanguage ToContentLanguage(UiLanguage? language) =>
        language == UiLanguage.Pl ? ContentLanguage.Pl : ContentLanguage.En;
}
