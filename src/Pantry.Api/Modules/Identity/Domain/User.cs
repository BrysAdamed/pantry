using Microsoft.AspNetCore.Identity;

namespace Pantry.Api.Modules.Identity.Domain;

public sealed class User : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 50;

    public required string DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public UiLanguage? UiLanguage { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>The username the user signs in with, or null while <c>UserName</c> is still the internal ID.</summary>
    public string? ChosenUserName => UserName == UserNames.Internal(Id) ? null : UserName;

    public bool HasPassword => PasswordHash is not null;

    public static User Create(Guid id, string displayName, UserRole role, string? userName = null) => new()
    {
        Id = id,
        UserName = userName ?? UserNames.Internal(id),
        DisplayName = displayName,
        Role = role,
    };

    public static User CreateAdmin(Guid id, string displayName, string? userName = null) =>
        Create(id, displayName, UserRole.Admin, userName);

    public static bool IsValidDisplayName(string displayName) => displayName.Length is > 0 and <= DisplayNameMaxLength;
}

public enum UserRole
{
    Admin,
    User,
}

public enum UiLanguage
{
    Pl,
    En,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}
