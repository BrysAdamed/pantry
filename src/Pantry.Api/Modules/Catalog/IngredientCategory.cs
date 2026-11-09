namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// The fixed, built-in set of ingredient categories (ADR 0019). The declaration order is the order of a shopping list
/// and of the category filter; labels live in the SPA under <c>catalog.category.&lt;code&gt;</c>.
/// </summary>
public enum IngredientCategory
{
    VegetablesAndFruit,
    DairyAndEggs,
    MeatAndFish,
    Bakery,
    DryAndCanned,
    Spices,
    Frozen,
    Drinks,
    Other,
}

public static class IngredientCategories
{
    public const IngredientCategory Default = IngredientCategory.Other;

    public static IReadOnlyList<IngredientCategory> Ordered { get; } = Enum.GetValues<IngredientCategory>();
}
