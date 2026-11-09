using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// One name an ingredient is known by: its name or an alias. <see cref="Key"/> is unique across the whole catalog,
/// so no two ingredients share a name or an alias, regardless of letter case and diacritics.
/// </summary>
public sealed partial class IngredientName
{
    internal IngredientName(string value, bool isAlias)
    {
        Value = value;
        Key = KeyOf(value);
        IsAlias = isAlias;
    }

    public string Value { get; internal set; }

    public string Key { get; private init; }

    public bool IsAlias { get; internal set; }

    /// <summary>Trims and collapses whitespace: the form a name is stored and shown in.</summary>
    public static string Clean(string value) => Whitespace().Replace(value.Trim(), " ");

    /// <summary>The comparison form: cleaned, lower case, without diacritics (<c>Żółty</c> and <c>zolty</c> share it).</summary>
    public static string KeyOf(string value)
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
