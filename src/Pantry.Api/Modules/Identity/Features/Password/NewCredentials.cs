using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Features.Password;

/// <summary>Checks a new username and password before they are saved: the username rules and Identity's password validators.</summary>
internal static class NewCredentials
{
    public const string UserNameField = "UserName";
    public const string PasswordField = "Password";

    /// <summary>Validates <paramref name="password"/> for <paramref name="user"/>, and <paramref name="userName"/> unless it is null (not asked for).</summary>
    public static async Task<ValidationProblem?> ValidateAsync(
        UserManager<User> userManager, User user, string? userName, string? password, string passwordField = PasswordField)
    {
        var errors = new Dictionary<string, string[]>();
        if (userName is not null && !UserNames.IsValid(userName))
        {
            errors[UserNameField] = [UserNames.Rule];
        }

        var passwordErrors = new List<string>();
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password ?? string.Empty);
            passwordErrors.AddRange(result.Errors.Select(e => e.Description));
        }

        if (passwordErrors.Count > 0)
        {
            errors[passwordField] = [.. passwordErrors];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    /// <summary>A taken username. Call under <c>LockUserAdministrationAsync</c>, which every change of a username takes.</summary>
    public static async Task<ValidationProblem?> EnsureUserNameFreeAsync(UserManager<User> userManager, string userName) =>
        await userManager.FindByNameAsync(userName) is null
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { [UserNameField] = ["The username is taken."] });

    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Changing the password failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
