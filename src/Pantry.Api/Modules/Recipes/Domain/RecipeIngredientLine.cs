using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// An ingredient line of a recipe: a catalog ingredient with a quantity and unit, or with neither (<i>to taste</i>), an
/// optional note, and the section it belongs to. <see cref="Quantity"/> is what was entered for the recipe servings;
/// <see cref="QuantityPerServing"/> is the same amount for one serving.
/// </summary>
public sealed class RecipeIngredientLine
{
    public const int NoteMaxLength = 200;

    internal RecipeIngredientLine(int position)
    {
        Position = position;
    }

    public int Position { get; private init; }

    /// <summary>The name of the section the line is in; null for a line outside any section.</summary>
    public string? Section { get; private set; }

    public Guid IngredientId { get; private set; }

    public decimal? Quantity { get; private set; }

    public decimal? QuantityPerServing { get; private set; }

    public Unit? Unit { get; private set; }

    public string? Note { get; private set; }

    public bool IsToTaste => Quantity is null;

    public static bool IsValidNote(string note) => note.Length <= NoteMaxLength;

    internal void Set(RecipeLine line, int servings)
    {
        Section = line.Section;
        IngredientId = line.IngredientId;
        Quantity = line.Quantity;
        QuantityPerServing = line.Quantity is { } quantity ? RecipeQuantities.PerServing(quantity, servings) : null;
        Unit = line.Unit;
        Note = line.Note;
    }
}

/// <summary>
/// An ingredient line as entered: <paramref name="Quantity"/> is for the recipe servings and comes with a
/// <paramref name="Unit"/>, or both are null for an ingredient added to taste.
/// </summary>
public sealed record RecipeLine(string? Section, Guid IngredientId, decimal? Quantity, Unit? Unit, string? Note);
