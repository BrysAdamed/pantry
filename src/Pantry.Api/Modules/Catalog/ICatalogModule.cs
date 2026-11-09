namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// In-process contract of the Catalog module. Recipes, Household (dislikes), Shopping and the MCP server read
/// ingredients through it and keep only their ids.
/// </summary>
public interface ICatalogModule
{
    /// <summary>The ingredient, or null when it does not exist (deleted or merged away).</summary>
    Task<CatalogIngredient?> GetIngredientAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The ingredients that exist among <paramref name="ids"/>, in catalog order. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogIngredient>> GetIngredientsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ingredient that has <paramref name="name"/> as its name or an alias, ignoring letter case, diacritics and
    /// extra whitespace; null when there is none.
    /// </summary>
    Task<CatalogIngredient?> FindIngredientAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="quantity"/> of <paramref name="unit"/> in the ingredient's base unit, using its own unit conversion
    /// before the generic one; <see cref="BaseQuantity.IsConverted"/> is false when neither exists. Null when the
    /// ingredient does not exist.
    /// </summary>
    Task<BaseQuantity?> ConvertToBaseUnitAsync(Guid ingredientId, decimal quantity, Unit unit, CancellationToken cancellationToken = default);
}

/// <summary>
/// An ingredient as other modules see it. <c>Aliases</c> are sorted; <c>BaseUnit</c> is <c>g</c>, <c>ml</c> or <c>piece</c>;
/// <c>Conversions</c> holds the amount of the base unit in one of each unit with an ingredient conversion.
/// </summary>
public sealed record CatalogIngredient(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    Unit BaseUnit,
    IngredientCategory Category,
    bool IsStaple,
    bool NeedsReview,
    IReadOnlyDictionary<Unit, decimal> Conversions)
{
    /// <summary>The quantity in the base unit; false when <paramref name="unit"/> has no ingredient or generic conversion into it.</summary>
    public bool TryConvertToBaseUnit(decimal quantity, Unit unit, out decimal converted) =>
        Units.TryConvert(quantity, unit, BaseUnit, Conversions, out converted);

    public BaseQuantity ConvertToBaseUnit(decimal quantity, Unit unit) =>
        new(BaseUnit, TryConvertToBaseUnit(quantity, unit, out var converted) ? converted : null);
}

/// <summary>A quantity converted into <see cref="BaseUnit"/>, or null <see cref="Quantity"/> when it cannot be converted.</summary>
public sealed record BaseQuantity(Unit BaseUnit, decimal? Quantity)
{
    public bool IsConverted => Quantity is not null;
}
