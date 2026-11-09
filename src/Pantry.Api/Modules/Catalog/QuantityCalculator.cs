using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// One line to sum, such as an ingredient line of a planned recipe scaled to its servings. <see cref="Quantity"/> and
/// <see cref="Unit"/> are both set or both null (an ingredient <i>to taste</i>).
/// </summary>
public sealed record QuantityLine(Guid IngredientId, decimal? Quantity, Unit? Unit)
{
    public static QuantityLine Of(Guid ingredientId, decimal quantity, Unit unit) => new(ingredientId, quantity, unit);

    public static QuantityLine ToTaste(Guid ingredientId) => new(ingredientId, null, null);
}

/// <summary>A quantity in a unit that could not be converted into the ingredient's base unit.</summary>
public sealed record UnitQuantity(Unit Unit, decimal Quantity);

/// <summary>
/// The sum of an ingredient's lines: <see cref="Total"/> in <see cref="BaseUnit"/> (null when no line converted into it)
/// and <see cref="OtherUnits"/>, the amounts without a conversion summed per unit in unit order, as in
/// <c>3 piece + 300 g</c>. An ingredient whose lines all lack a quantity has neither.
/// </summary>
public sealed record IngredientTotal(
    Guid IngredientId,
    string Name,
    Unit BaseUnit,
    decimal? Total,
    IReadOnlyList<UnitQuantity> OtherUnits,
    IngredientCategory Category,
    bool IsStaple)
{
    public bool HasQuantity => Total is not null || OtherUnits.Count > 0;
}

/// <summary>
/// The totals per ingredient in shopping-list order (category, then name), and the ids of lines whose ingredient is not
/// in the catalog (deleted or merged away), in input order.
/// </summary>
public sealed record QuantityTotals(IReadOnlyList<IngredientTotal> Ingredients, IReadOnlyList<Guid> UnknownIngredientIds);

/// <summary>
/// Sums quantities per ingredient into its base unit (ADR 0008), with the ingredient's own unit conversions before the
/// generic ones. Decimals are kept as they are; rounding to packs is left to agents.
/// </summary>
public static class QuantityCalculator
{
    public static QuantityTotals Sum(IReadOnlyCollection<QuantityLine> lines, IReadOnlyCollection<CatalogIngredient> ingredients)
    {
        foreach (var line in lines)
        {
            Validate(line);
        }

        var catalog = ingredients.ToDictionary(i => i.Id);
        var totals = lines
            .Where(line => catalog.ContainsKey(line.IngredientId))
            .GroupBy(line => line.IngredientId)
            .Select(group => Total(catalog[group.Key], group))
            .OrderBy(total => total.Category)
            .ThenBy(total => IngredientName.KeyOf(total.Name), StringComparer.Ordinal)
            .ThenBy(total => total.IngredientId)
            .ToList();
        var unknown = lines.Select(line => line.IngredientId).Where(id => !catalog.ContainsKey(id)).Distinct().ToList();
        return new QuantityTotals(totals, unknown);
    }

    private static IngredientTotal Total(CatalogIngredient ingredient, IEnumerable<QuantityLine> lines)
    {
        decimal? total = null;
        var otherUnits = new SortedDictionary<Unit, decimal>();
        foreach (var line in lines)
        {
            if (line is not { Quantity: { } quantity, Unit: { } unit })
            {
                continue;
            }

            if (ingredient.TryConvertToBaseUnit(quantity, unit, out var converted))
            {
                total = (total ?? 0m) + converted;
            }
            else
            {
                otherUnits[unit] = otherUnits.GetValueOrDefault(unit) + quantity;
            }
        }

        return new IngredientTotal(
            ingredient.Id,
            ingredient.Name,
            ingredient.BaseUnit,
            total,
            otherUnits.Select(p => new UnitQuantity(p.Key, p.Value)).ToList(),
            ingredient.Category,
            ingredient.IsStaple);
    }

    private static void Validate(QuantityLine line)
    {
        if (line.Quantity.HasValue != line.Unit.HasValue)
        {
            throw new ArgumentException($"A line of {line.IngredientId} has a quantity without a unit or a unit without a quantity.", nameof(line));
        }

        if (line.Quantity is <= 0)
        {
            throw new ArgumentException($"A line of {line.IngredientId} has a quantity that is not greater than 0.", nameof(line));
        }
    }
}
