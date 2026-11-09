namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>A free-text label of a recipe (<c>obiad</c>, <c>szybkie</c>), stored trimmed and in lower case.</summary>
public sealed class RecipeTag
{
    public const int MaxLength = 40;

    internal RecipeTag(string value)
    {
        Value = value;
    }

    public string Value { get; private init; }

    /// <summary>Trimmed, inner whitespace collapsed to one space, lower case.</summary>
    public static string Normalize(string tag) =>
        string.Join(' ', tag.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static bool IsValid(string normalizedTag) => normalizedTag.Length is > 0 and <= MaxLength;
}
