namespace Pantry.Api.Modules.Identity.Domain;

/// <summary>Role claim values, usable with <c>RequireRole</c>.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string User = "user";

    public static string Of(UserRole role) => role switch
    {
        UserRole.Admin => Admin,
        UserRole.User => User,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
