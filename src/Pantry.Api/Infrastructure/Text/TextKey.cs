using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pantry.Api.Infrastructure.Text;

/// <summary>
/// The comparison form of names people type and search for: trimmed, whitespace collapsed, lower case, without
/// diacritics (<c>Żółty</c> and <c>zolty</c> share it). Modules store it next to a name to match and search ignoring
/// letter case and diacritics.
/// </summary>
public static partial class TextKey
{
    /// <summary>Trims and collapses whitespace: the form a name is stored and shown in.</summary>
    public static string Clean(string value) => Whitespace().Replace(value.Trim(), " ");

    public static string Of(string value)
    {
        var decomposed = Clean(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var key = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            key.Append(character switch
            {
                'ł' => "l",
                'đ' => "d",
                'ø' => "o",
                'ß' => "ss",
                'æ' => "ae",
                'œ' => "oe",
                _ => character.ToString(),
            });
        }

        return key.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
