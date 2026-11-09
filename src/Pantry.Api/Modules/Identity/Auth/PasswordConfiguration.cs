using Microsoft.AspNetCore.Identity;
using Pantry.Api.Modules.Identity.Domain;

namespace Pantry.Api.Modules.Identity.Auth;

/// <summary>Password policy and lockout of ADR 0018: length only, no character-type rules; 15 minutes after 5 failures.</summary>
internal static class PasswordConfiguration
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void Apply(IdentityOptions options)
    {
        options.Password.RequiredLength = MinLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
    }
}

internal sealed class MaxLengthPasswordValidator : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password) =>
        Task.FromResult(password?.Length > PasswordConfiguration.MaxLength
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"Passwords must be at most {PasswordConfiguration.MaxLength} characters.",
            })
            : IdentityResult.Success);
}
