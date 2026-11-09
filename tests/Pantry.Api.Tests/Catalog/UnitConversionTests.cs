using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Tests.Catalog;

public sealed class UnitConversionTests
{
    [Fact]
    public void Ingredient_conversion_takes_precedence_over_the_generic_one()
    {
        // Arrange
        var flour = Contract(Unit.G, (Unit.Tbsp, 10m));

        // Act
        var converted = flour.TryConvertToBaseUnit(2m, Unit.Tbsp, out var grams);

        // Assert
        Assert.NotNull(Units.GenericConversionOf(Unit.Tbsp));
        Assert.True(converted);
        Assert.Equal(20m, grams);
    }

    [Fact]
    public void Ingredient_conversion_is_used_for_units_without_a_generic_one()
    {
        var garlic = Contract(Unit.G, (Unit.Clove, 5m), (Unit.Piece, 50m));

        Assert.Equal(new BaseQuantity(Unit.G, 15m), garlic.ConvertToBaseUnit(3m, Unit.Clove));
        Assert.Equal(new BaseQuantity(Unit.G, 25m), garlic.ConvertToBaseUnit(0.5m, Unit.Piece));
    }

    [Theory]
    [InlineData(Unit.G, 250, 250)]
    [InlineData(Unit.Kg, 1.25, 1250)]
    public void Generic_conversions_still_apply_next_to_ingredient_ones(Unit unit, double quantity, double expected)
    {
        var garlic = Contract(Unit.G, (Unit.Clove, 5m));

        Assert.True(garlic.TryConvertToBaseUnit((decimal)quantity, unit, out var converted));
        Assert.Equal((decimal)expected, converted);
    }

    [Theory]
    [InlineData(Unit.Can)]
    [InlineData(Unit.Ml)]
    [InlineData(Unit.Tsp)]
    [InlineData(Unit.Pinch)]
    public void Units_without_any_conversion_into_the_base_unit_are_not_converted(Unit unit)
    {
        var garlic = Contract(Unit.G, (Unit.Clove, 5m));

        var result = garlic.ConvertToBaseUnit(1m, unit);

        Assert.False(result.IsConverted);
        Assert.Null(result.Quantity);
        Assert.Equal(Unit.G, result.BaseUnit);
    }

    [Fact]
    public void Decimals_are_kept()
    {
        var tomatoes = Contract(Unit.G, (Unit.Can, 400m), (Unit.Pinch, 0.3m));

        Assert.Equal(533.3333m, tomatoes.ConvertToBaseUnit(1.3333333m, Unit.Can).Quantity!.Value, 4);
        Assert.Equal(0.45m, tomatoes.ConvertToBaseUnit(1.5m, Unit.Pinch).Quantity);
    }

    [Theory]
    [InlineData(Unit.G, Unit.G)]
    [InlineData(Unit.Kg, Unit.G)]
    [InlineData(Unit.Ml, Unit.Ml)]
    [InlineData(Unit.L, Unit.Ml)]
    [InlineData(Unit.Tbsp, Unit.Ml)]
    [InlineData(Unit.Tsp, Unit.Ml)]
    [InlineData(Unit.Piece, Unit.Piece)]
    public void Conversions_to_the_base_unit_or_within_its_generic_family_are_redundant(Unit unit, Unit baseUnit)
    {
        Assert.True(UnitConversion.IsRedundant(unit, baseUnit));
        Assert.NotEmpty(Ingredient.ConversionProblems(baseUnit, [(unit, 1m)]));
    }

    [Theory]
    [InlineData(Unit.Tbsp, Unit.G)]
    [InlineData(Unit.Ml, Unit.G)]
    [InlineData(Unit.G, Unit.Ml)]
    [InlineData(Unit.Piece, Unit.G)]
    [InlineData(Unit.Kg, Unit.Piece)]
    [InlineData(Unit.Clove, Unit.G)]
    [InlineData(Unit.Can, Unit.Piece)]
    public void Conversions_across_families_or_from_ingredient_units_are_allowed(Unit unit, Unit baseUnit) =>
        Assert.Empty(Ingredient.ConversionProblems(baseUnit, [(unit, 1m)]));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(0.00001)]
    [InlineData(1000000.5)]
    public void Amounts_must_be_positive_bounded_and_have_at_most_four_decimal_places(double amount) =>
        Assert.NotEmpty(Ingredient.ConversionProblems(Unit.G, [(Unit.Clove, (decimal)amount)]));

    [Fact]
    public void A_unit_has_one_conversion() =>
        Assert.NotEmpty(Ingredient.ConversionProblems(Unit.G, [(Unit.Clove, 5m), (Unit.Clove, 6m)]));

    [Fact]
    public void Set_conversions_adds_changes_and_removes_keeping_rows_of_kept_units()
    {
        // Arrange
        var garlic = Ingredient.Create("Czosnek", [], Unit.G);
        garlic.SetConversions([(Unit.Clove, 5m), (Unit.Piece, 50m)]);
        var clove = garlic.Conversions.Single(c => c.Unit == Unit.Clove);

        // Act
        garlic.SetConversions([(Unit.Clove, 6m), (Unit.Jar, 120m)]);

        // Assert
        Assert.Equal(new Dictionary<Unit, decimal> { [Unit.Clove] = 6m, [Unit.Jar] = 120m }, garlic.ConversionAmounts());
        Assert.Same(clove, garlic.Conversions.Single(c => c.Unit == Unit.Clove));
    }

    [Fact]
    public void Set_conversions_rejects_invalid_ones()
    {
        var garlic = Ingredient.Create("Czosnek", [], Unit.G);

        Assert.Throws<ArgumentException>(() => garlic.SetConversions([(Unit.Kg, 1000m)]));
        Assert.Throws<ArgumentException>(() => garlic.SetConversions([(Unit.Clove, 0m)]));
    }

    [Fact]
    public void Changing_the_base_unit_removes_the_conversions()
    {
        // Arrange
        var garlic = Ingredient.Create("Czosnek", [], Unit.G);
        garlic.SetConversions([(Unit.Clove, 5m)]);

        // Act
        garlic.SetBaseUnit(Unit.G);
        var kept = garlic.Conversions.Count;
        garlic.SetBaseUnit(Unit.Piece);

        // Assert
        Assert.Equal(1, kept);
        Assert.Equal(Unit.Piece, garlic.BaseUnit);
        Assert.Empty(garlic.Conversions);
        Assert.Throws<ArgumentException>(() => garlic.SetBaseUnit(Unit.Kg));
    }

    private static CatalogIngredient Contract(Unit baseUnit, params (Unit Unit, decimal Amount)[] conversions) => new(
        Guid.NewGuid(), "Składnik", [], baseUnit, IngredientCategory.Other, false, false,
        conversions.ToDictionary(c => c.Unit, c => c.Amount));
}
