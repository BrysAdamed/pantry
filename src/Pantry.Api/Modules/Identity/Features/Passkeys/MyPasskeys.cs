using System.Buffers.Text;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Pantry.Api.Modules.Identity.Features.Passkeys;

/// <summary>A passkey of the signed-in user. The id is the credential id, base64url, as WebAuthn writes it.</summary>
public sealed record PasskeyResponse(string Id, string? Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

internal static class MyPasskeys
{
    public const int NameMaxLength = 50;

    public static string ToId(byte[] credentialId) => Base64Url.EncodeToString(credentialId);

    public static byte[]? FromId(string id)
    {
        try
        {
            return Base64Url.DecodeFromChars(id);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string? NormalizeName(string? name) => name?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    public static bool IsValidName(string? name) => name is null || name.Length <= NameMaxLength;

    public static ValidationProblem InvalidName() => TypedResults.ValidationProblem(new Dictionary<string, string[]>
    {
        ["Name"] = [$"Passkey name must have 1 to {NameMaxLength} characters."],
    });
}
