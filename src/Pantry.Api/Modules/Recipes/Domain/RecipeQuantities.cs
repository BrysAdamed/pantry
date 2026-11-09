namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// Quantities of a recipe are entered for its servings and stored per one serving as well, so the planner and the
/// shopping list scale them without knowing the servings the recipe was written for.
/// </summary>
public static class RecipeQuantities
{
    public const decimal MinQuantity = 0.001m;
    public const decimal MaxQuantity = 100_000m;
    public const int QuantityScale = 3;
    public const int PerServingScale = 6;

    public static bool IsValidQuantity(decimal quantity) =>
        quantity is >= MinQuantity and <= MaxQuantity && decimal.Round(quantity, QuantityScale) == quantity;

    /// <summary>
    /// <paramref name="quantity"/> entered for <paramref name="servings"/>, divided down to one serving and rounded to
    /// <see cref="PerServingScale"/> decimal places.
    /// </summary>
    public static decimal PerServing(decimal quantity, int servings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(servings, 1);
        return decimal.Round(quantity / servings, PerServingScale, MidpointRounding.AwayFromZero);
    }
}
