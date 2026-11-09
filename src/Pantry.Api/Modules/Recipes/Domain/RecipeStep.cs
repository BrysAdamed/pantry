namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>One step of the method, in its place in the order.</summary>
public sealed class RecipeStep
{
    public const int TextMaxLength = 4000;

    internal RecipeStep(int position, string text)
    {
        Position = position;
        Text = text;
    }

    public int Position { get; private init; }

    public string Text { get; internal set; }

    public static bool IsValidText(string text) => text.Length is > 0 and <= TextMaxLength;
}
