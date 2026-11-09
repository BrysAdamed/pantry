using Pantry.Api.Infrastructure.Text;

namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// One name an ingredient is known by: its name or an alias. <see cref="Key"/> is unique across the whole catalog,
/// so no two ingredients share a name or an alias, regardless of letter case and diacritics.
/// </summary>
public sealed class IngredientName
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
    public static string Clean(string value) => TextKey.Clean(value);

    /// <summary>The comparison form (<see cref="TextKey.Of"/>): <c>Żółty</c> and <c>zolty</c> share it.</summary>
    public static string KeyOf(string value) => TextKey.Of(value);
}
