using Microsoft.AspNetCore.Identity;

namespace Pantry.Api.Modules.Identity.Domain;

public sealed class User : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 50;

    public required string DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public UiLanguage? UiLanguage { get; set; }

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public static User Create(Guid id, string displayName, UserRole role) => new()
    {
        Id = id,
        UserName = id.ToString("N"),
        DisplayName = displayName,
        Role = role,
    };

    public static User CreateAdmin(Guid id, string displayName) => Create(id, displayName, UserRole.Admin);

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
