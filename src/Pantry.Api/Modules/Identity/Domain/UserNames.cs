using System.Text;
using System.Text.RegularExpressions;

namespace Pantry.Api.Modules.Identity.Domain;

/// <summary>
/// Usernames for password sign-in (ADR 0018): unique and case-insensitive, 3 to 32 letters, digits, <c>.</c>, <c>_</c> or
/// <c>-</c>, and never like the internal ID that a user without a username has as <c>UserName</c>.
/// </summary>
public static partial class UserNames
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    public const string Rule = "A username has 3 to 32 letters, digits, '.', '_' or '-', and is not 32 hexadecimal characters.";

    public static string Internal(Guid userId) => userId.ToString("N");

    public static string Clean(string? userName) => (userName ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);

    public static bool IsValid(string userName) => Allowed().IsMatch(userName) && !LikeInternal().IsMatch(userName);

    [GeneratedRegex(@"^[\p{L}0-9._-]{3,32}\z")]
    private static partial Regex Allowed();

    [GeneratedRegex(@"^[0-9a-fA-F]{32}\z")]
    private static partial Regex LikeInternal();
}
