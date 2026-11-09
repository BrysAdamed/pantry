using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// The text code of a catalog enum value, as the API, the database and the SPA i18n keys use it: the camelCase name
/// (<c>vegetablesAndFruit</c>, <c>tbsp</c>). Numbers are never accepted.
/// </summary>
public static class CatalogCode
{
    public static string Of<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParse<TEnum>(string? code, [NotNullWhen(true)] out TEnum? value) where TEnum : struct, Enum
    {
        var trimmed = code?.Trim();
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Of(candidate), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }

    public static TEnum Parse<TEnum>(string code) where TEnum : struct, Enum =>
        TryParse<TEnum>(code, out var value) ? value.Value : throw new FormatException($"'{code}' is not a {typeof(TEnum).Name} code.");
}
