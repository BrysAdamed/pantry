namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// The fixed vocabulary of units a quantity can be written in (ADR 0019). The camelCase name is the code
/// (<c>g</c>, <c>kg</c>, <c>tbsp</c>); labels live in the SPA under <c>catalog.unit.&lt;code&gt;</c>.
/// </summary>
public enum Unit
{
    G,
    Kg,
    Ml,
    L,
    Piece,
    Clove,
    Tbsp,
    Tsp,
    Pinch,
    Can,
    Jar,
    Pack,
    Slice,
    Bunch,
    Handful,
    Glass,
}

/// <summary>A conversion that holds for every ingredient: one <c>unit</c> is <see cref="Factor"/> of <see cref="BaseUnit"/>.</summary>
public sealed record GenericConversion(Unit BaseUnit, decimal Factor);

public static class Units
{
    private static readonly Dictionary<Unit, GenericConversion> GenericConversions = new()
    {
        [Unit.G] = new(Unit.G, 1m),
        [Unit.Kg] = new(Unit.G, 1000m),
        [Unit.Ml] = new(Unit.Ml, 1m),
        [Unit.L] = new(Unit.Ml, 1000m),
        [Unit.Tbsp] = new(Unit.Ml, 15m),
        [Unit.Tsp] = new(Unit.Ml, 5m),
        [Unit.Piece] = new(Unit.Piece, 1m),
    };

    public static IReadOnlyList<Unit> Ordered { get; } = Enum.GetValues<Unit>();

    /// <summary>The units an ingredient can be summed in.</summary>
    public static IReadOnlyList<Unit> BaseUnits { get; } = [Unit.G, Unit.Ml, Unit.Piece];

    public static bool IsBaseUnit(Unit unit) => BaseUnits.Contains(unit);

    /// <summary>
    /// The base unit of an ingredient first seen with <paramref name="unitUsed"/>: <c>g</c> for weights, <c>ml</c> for
    /// volumes, <c>piece</c> for everything else (a clove, a can, no unit at all).
    /// </summary>
    public static Unit GuessBaseUnit(Unit? unitUsed) =>
        unitUsed is { } unit && GenericConversionOf(unit) is { } conversion ? conversion.BaseUnit : Unit.Piece;

    /// <summary>The built-in conversion of <paramref name="unit"/>, or null when it depends on the ingredient (a clove, a can).</summary>
    public static GenericConversion? GenericConversionOf(Unit unit) => GenericConversions.GetValueOrDefault(unit);

    /// <summary>Converts with the built-in conversions only. False when <paramref name="unit"/> has none into <paramref name="baseUnit"/>.</summary>
    public static bool TryConvertGeneric(decimal quantity, Unit unit, Unit baseUnit, out decimal converted)
    {
        if (GenericConversionOf(unit) is { } conversion && conversion.BaseUnit == baseUnit)
        {
            converted = quantity * conversion.Factor;
            return true;
        }

        converted = 0;
        return false;
    }

    /// <summary>
    /// Converts into <paramref name="baseUnit"/> with the ingredient's own <paramref name="conversions"/> (amount of the
    /// base unit per one unit) first and the built-in ones second. False when neither has <paramref name="unit"/>.
    /// </summary>
    public static bool TryConvert(
        decimal quantity, Unit unit, Unit baseUnit, IReadOnlyDictionary<Unit, decimal> conversions, out decimal converted)
    {
        if (conversions.TryGetValue(unit, out var amount))
        {
            converted = quantity * amount;
            return true;
        }

        return TryConvertGeneric(quantity, unit, baseUnit, out converted);
    }
}
