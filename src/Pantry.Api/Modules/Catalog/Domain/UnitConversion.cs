namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>An ingredient's own rule <c>1 <see cref="Unit"/> = <see cref="Amount"/> of its base unit</c>, like 1 clove = 5 g.</summary>
public sealed class UnitConversion
{
    public const decimal MaxAmount = 1_000_000m;
    public const int AmountScale = 4;

    internal UnitConversion(Unit unit, decimal amount)
    {
        Unit = unit;
        Amount = amount;
    }

    public Unit Unit { get; private init; }

    public decimal Amount { get; internal set; }

    public static bool IsValidAmount(decimal amount) =>
        amount is > 0 and <= MaxAmount && decimal.Round(amount, AmountScale) == amount;

    /// <summary>
    /// True when <paramref name="unit"/> needs no ingredient conversion into <paramref name="baseUnit"/>: it is the base
    /// unit itself or has a generic conversion into it (<c>kg</c> for <c>g</c>, <c>tbsp</c> for <c>ml</c>).
    /// </summary>
    public static bool IsRedundant(Unit unit, Unit baseUnit) => Units.GenericConversionOf(unit)?.BaseUnit == baseUnit;
}
