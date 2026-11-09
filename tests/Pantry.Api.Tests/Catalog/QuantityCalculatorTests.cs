using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Tests.Catalog;

public sealed class QuantityCalculatorTests
{
    private static readonly CatalogIngredient Flour = Ingredient("Mąka", Unit.G, IngredientCategory.DryAndCanned, (Unit.Tbsp, 10m), (Unit.Glass, 130m));
    private static readonly CatalogIngredient Milk = Ingredient("Mleko", Unit.Ml, IngredientCategory.DairyAndEggs);
    private static readonly CatalogIngredient Garlic = Ingredient("Czosnek", Unit.G, IngredientCategory.VegetablesAndFruit, (Unit.Clove, 5m));
    private static readonly CatalogIngredient Onion = Ingredient("Cebula", Unit.Piece, IngredientCategory.VegetablesAndFruit);
    private static readonly CatalogIngredient Salt = Ingredient("Sól", Unit.G, IngredientCategory.Spices, isStaple: true);

    private static readonly CatalogIngredient[] Catalog = [Flour, Milk, Garlic, Onion, Salt];

    [Fact]
    public void Generic_conversions_sum_into_the_base_unit()
    {
        var totals = Sum(Line(Milk, 0.5m, Unit.L), Line(Milk, 200m, Unit.Ml), Line(Milk, 2m, Unit.Tbsp), Line(Milk, 1m, Unit.Tsp));

        var milk = Assert.Single(totals.Ingredients);
        Assert.Equal(Unit.Ml, milk.BaseUnit);
        Assert.Equal(735m, milk.Total);
        Assert.Empty(milk.OtherUnits);
    }

    [Fact]
    public void Ingredient_conversions_sum_into_the_base_unit()
    {
        var totals = Sum(Line(Garlic, 3m, Unit.Clove), Line(Garlic, 0.02m, Unit.Kg), Line(Garlic, 10m, Unit.G));

        Assert.Equal(45m, Assert.Single(totals.Ingredients).Total);
    }

    [Fact]
    public void Ingredient_conversion_takes_precedence_over_the_generic_one()
    {
        var totals = Sum(Line(Flour, 2m, Unit.Tbsp));

        Assert.Equal(20m, Assert.Single(totals.Ingredients).Total);
    }

    [Fact]
    public void Generic_conversion_into_another_base_unit_is_not_used()
    {
        var totals = Sum(Line(Garlic, 1m, Unit.Tbsp), Line(Milk, 100m, Unit.G));

        var garlic = totals.Ingredients.Single(t => t.IngredientId == Garlic.Id);
        var milk = totals.Ingredients.Single(t => t.IngredientId == Milk.Id);
        Assert.Null(garlic.Total);
        Assert.Equal([new UnitQuantity(Unit.Tbsp, 1m)], garlic.OtherUnits);
        Assert.Null(milk.Total);
        Assert.Equal([new UnitQuantity(Unit.G, 100m)], milk.OtherUnits);
    }

    [Fact]
    public void Unconvertible_amounts_are_summed_per_unit_in_unit_order_next_to_the_total()
    {
        // Arrange
        var lines = new[]
        {
            Line(Onion, 2m, Unit.Piece),
            Line(Onion, 300m, Unit.G),
            Line(Onion, 1m, Unit.Piece),
            Line(Onion, 1m, Unit.Bunch),
            Line(Onion, 0.2m, Unit.Kg),
        };

        // Act
        var onion = Assert.Single(Sum(lines).Ingredients);

        // Assert
        Assert.Equal(3m, onion.Total);
        Assert.Equal([new UnitQuantity(Unit.G, 300m), new UnitQuantity(Unit.Kg, 0.2m), new UnitQuantity(Unit.Bunch, 1m)], onion.OtherUnits);
    }

    [Fact]
    public void Mixed_lines_of_several_ingredients_are_summed_separately()
    {
        var totals = Sum(
            Line(Flour, 500m, Unit.G),
            Line(Milk, 1m, Unit.Glass),
            Line(Flour, 1m, Unit.Glass),
            Line(Garlic, 2m, Unit.Clove),
            Line(Milk, 250m, Unit.Ml),
            Line(Flour, 1m, Unit.Pinch));

        var flour = totals.Ingredients.Single(t => t.IngredientId == Flour.Id);
        var milk = totals.Ingredients.Single(t => t.IngredientId == Milk.Id);
        var garlic = totals.Ingredients.Single(t => t.IngredientId == Garlic.Id);
        Assert.Equal(630m, flour.Total);
        Assert.Equal([new UnitQuantity(Unit.Pinch, 1m)], flour.OtherUnits);
        Assert.Equal(250m, milk.Total);
        Assert.Equal([new UnitQuantity(Unit.Glass, 1m)], milk.OtherUnits);
        Assert.Equal(10m, garlic.Total);
    }

    [Fact]
    public void Decimals_are_kept_without_rounding()
    {
        var totals = Sum(Line(Flour, 0.3333m, Unit.Tbsp), Line(Flour, 0.0001m, Unit.Kg), Line(Milk, 1.0005m, Unit.L), Line(Onion, 0.25m, Unit.Can), Line(Onion, 0.125m, Unit.Can));

        Assert.Equal(3.4330m, totals.Ingredients.Single(t => t.IngredientId == Flour.Id).Total);
        Assert.Equal(1000.5m, totals.Ingredients.Single(t => t.IngredientId == Milk.Id).Total);
        Assert.Equal([new UnitQuantity(Unit.Can, 0.375m)], totals.Ingredients.Single(t => t.IngredientId == Onion.Id).OtherUnits);
    }

    [Fact]
    public void Unknown_ingredients_are_reported_once_and_left_out_of_the_totals()
    {
        var deleted = Guid.NewGuid();
        var merged = Guid.NewGuid();

        var totals = Sum(Line(deleted, 1m, Unit.G), Line(Milk, 100m, Unit.Ml), Line(merged, 2m, Unit.Piece), QuantityLine.ToTaste(deleted));

        Assert.Equal(Milk.Id, Assert.Single(totals.Ingredients).IngredientId);
        Assert.Equal([deleted, merged], totals.UnknownIngredientIds);
    }

    [Fact]
    public void Ingredient_with_only_to_taste_lines_has_no_amount()
    {
        var totals = Sum(QuantityLine.ToTaste(Salt.Id), QuantityLine.ToTaste(Salt.Id));

        var salt = Assert.Single(totals.Ingredients);
        Assert.Null(salt.Total);
        Assert.Empty(salt.OtherUnits);
        Assert.False(salt.HasQuantity);
    }

    [Fact]
    public void To_taste_line_does_not_hide_amounts_from_other_lines()
    {
        var totals = Sum(QuantityLine.ToTaste(Garlic.Id), Line(Garlic, 2m, Unit.Clove));

        var garlic = Assert.Single(totals.Ingredients);
        Assert.Equal(10m, garlic.Total);
        Assert.True(garlic.HasQuantity);
    }

    [Fact]
    public void Totals_carry_name_category_and_staple_flag_in_shopping_list_order()
    {
        var totals = Sum(Line(Salt, 5m, Unit.G), Line(Milk, 1m, Unit.L), Line(Onion, 1m, Unit.Piece), Line(Garlic, 1m, Unit.Clove));

        Assert.Equal(["Cebula", "Czosnek", "Mleko", "Sól"], totals.Ingredients.Select(t => t.Name));
        Assert.Equal(
            [IngredientCategory.VegetablesAndFruit, IngredientCategory.VegetablesAndFruit, IngredientCategory.DairyAndEggs, IngredientCategory.Spices],
            totals.Ingredients.Select(t => t.Category));
        Assert.Equal([false, false, false, true], totals.Ingredients.Select(t => t.IsStaple));
    }

    [Fact]
    public void No_lines_give_no_totals()
    {
        var totals = Sum();

        Assert.Empty(totals.Ingredients);
        Assert.Empty(totals.UnknownIngredientIds);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, Unit.G)]
    [InlineData(0, Unit.G)]
    [InlineData(-1, Unit.G)]
    public void Invalid_lines_are_rejected(int? quantity, Unit? unit) =>
        Assert.Throws<ArgumentException>(() => Sum(new QuantityLine(Milk.Id, quantity, unit)));

    private static QuantityTotals Sum(params QuantityLine[] lines) => QuantityCalculator.Sum(lines, Catalog);

    private static QuantityLine Line(CatalogIngredient ingredient, decimal quantity, Unit unit) => Line(ingredient.Id, quantity, unit);

    private static QuantityLine Line(Guid ingredientId, decimal quantity, Unit unit) => QuantityLine.Of(ingredientId, quantity, unit);

    private static CatalogIngredient Ingredient(
        string name, Unit baseUnit, IngredientCategory category, params (Unit Unit, decimal Amount)[] conversions) =>
        Ingredient(name, baseUnit, category, false, conversions);

    private static CatalogIngredient Ingredient(
        string name, Unit baseUnit, IngredientCategory category, bool isStaple, params (Unit Unit, decimal Amount)[] conversions) =>
        new(Guid.NewGuid(), name, [], baseUnit, category, isStaple, false, conversions.ToDictionary(c => c.Unit, c => c.Amount));
}
