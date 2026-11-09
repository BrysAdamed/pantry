using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Tests.Catalog;

public sealed class CatalogDomainTests
{
    [Theory]
    [InlineData("Cebula", "cebula")]
    [InlineData("żółty ser", "zolty ser")]
    [InlineData("  Ser   ŻÓŁTY ", "ser zolty")]
    [InlineData("Crème fraîche", "creme fraiche")]
    [InlineData("Ćwikła", "cwikla")]
    [InlineData("Weißwurst", "weisswurst")]
    public void Name_key_ignores_case_diacritics_and_extra_whitespace(string name, string key) =>
        Assert.Equal(key, IngredientName.KeyOf(name));

    [Fact]
    public void Clean_trims_and_collapses_whitespace() =>
        Assert.Equal("Ser żółty", IngredientName.Clean("  Ser \t  żółty "));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(Ingredient.NameMaxLength, true)]
    [InlineData(Ingredient.NameMaxLength + 1, false)]
    public void Name_has_1_to_100_characters(int length, bool valid) =>
        Assert.Equal(valid, Ingredient.IsValidName(new string('a', length)));

    [Fact]
    public void Create_cleans_names_and_sorts_aliases()
    {
        var ingredient = Ingredient.Create(" Cebula  biała ", ["Cebulka", "Biała cebula"], Unit.Piece);

        Assert.Equal("Cebula biała", ingredient.Name);
        Assert.Equal("cebula biala", ingredient.NameKey);
        Assert.Equal(["Biała cebula", "Cebulka"], ingredient.Aliases);
        Assert.Equal(IngredientCategory.Other, ingredient.Category);
        Assert.False(ingredient.NeedsReview);
    }

    [Theory]
    [InlineData("Cebula", "cebula")]
    [InlineData("Żółty ser", "zolty ser")]
    public void Names_that_differ_only_in_case_or_diacritics_are_repeated(string name, string alias) =>
        Assert.Equal([name], Ingredient.RepeatedNames(name, [alias]));

    [Fact]
    public void Create_rejects_repeated_names_and_units_that_are_not_base_units()
    {
        Assert.Throws<ArgumentException>(() => Ingredient.Create("Cebula", ["CEBULA"], Unit.Piece));
        Assert.Throws<ArgumentException>(() => Ingredient.Create("Mąka", [], Unit.Kg));
    }

    [Fact]
    public void Swapping_the_name_with_an_alias_keeps_both_name_rows()
    {
        // Arrange
        var ingredient = Ingredient.Create("Cebula", ["Cebulka"], Unit.Piece);
        var rows = ingredient.Names.ToList();

        // Act
        ingredient.SetNames("cebulka", ["Cebula", "Szalotka"]);

        // Assert
        Assert.Equal("cebulka", ingredient.Name);
        Assert.Equal(["Cebula", "Szalotka"], ingredient.Aliases);
        Assert.Equal(3, ingredient.Names.Count);
        Assert.All(rows, row => Assert.Contains(row, ingredient.Names));
        Assert.False(ingredient.Names.Single(n => n.Key == "cebulka").IsAlias);
    }

    [Fact]
    public void Removed_aliases_lose_their_rows()
    {
        var ingredient = Ingredient.Create("Cebula", ["Cebulka", "Szalotka"], Unit.Piece);

        ingredient.SetNames("Cebula", ["Szalotka"]);

        Assert.Equal(["cebula", "szalotka"], ingredient.Names.Select(n => n.Key).Order());
    }

    [Theory]
    [InlineData(Unit.G, 250, Unit.G, 250)]
    [InlineData(Unit.Kg, 1.5, Unit.G, 1500)]
    [InlineData(Unit.L, 0.25, Unit.Ml, 250)]
    [InlineData(Unit.Tbsp, 2, Unit.Ml, 30)]
    [InlineData(Unit.Tsp, 0.5, Unit.Ml, 2.5)]
    [InlineData(Unit.Piece, 3, Unit.Piece, 3)]
    public void Generic_conversions_are_built_in(Unit unit, double quantity, Unit baseUnit, double expected)
    {
        Assert.True(Units.TryConvertGeneric((decimal)quantity, unit, baseUnit, out var converted));
        Assert.Equal((decimal)expected, converted);
    }

    [Theory]
    [InlineData(Unit.Tbsp, Unit.G)]
    [InlineData(Unit.Kg, Unit.Ml)]
    [InlineData(Unit.Clove, Unit.G)]
    [InlineData(Unit.Can, Unit.Piece)]
    [InlineData(Unit.Pinch, Unit.G)]
    public void Units_without_a_generic_conversion_into_the_base_unit_are_not_converted(Unit unit, Unit baseUnit) =>
        Assert.False(Units.TryConvertGeneric(1m, unit, baseUnit, out _));

    [Theory]
    [InlineData(Unit.G, Unit.G)]
    [InlineData(Unit.Kg, Unit.G)]
    [InlineData(Unit.Ml, Unit.Ml)]
    [InlineData(Unit.L, Unit.Ml)]
    [InlineData(Unit.Tbsp, Unit.Ml)]
    [InlineData(Unit.Tsp, Unit.Ml)]
    [InlineData(Unit.Piece, Unit.Piece)]
    [InlineData(Unit.Clove, Unit.Piece)]
    [InlineData(Unit.Can, Unit.Piece)]
    [InlineData(Unit.Pinch, Unit.Piece)]
    [InlineData(Unit.Glass, Unit.Piece)]
    [InlineData(null, Unit.Piece)]
    public void Base_unit_is_guessed_from_the_unit_used(Unit? unitUsed, Unit expected) =>
        Assert.Equal(expected, Units.GuessBaseUnit(unitUsed));

    [Fact]
    public void Ingredient_from_an_unknown_name_needs_review_until_confirmed()
    {
        var ingredient = Ingredient.CreateFromUnknownName(" Imbir ", Unit.Kg);

        Assert.Equal("Imbir", ingredient.Name);
        Assert.Equal(Unit.G, ingredient.BaseUnit);
        Assert.Equal(IngredientCategory.Other, ingredient.Category);
        Assert.Empty(ingredient.Aliases);
        Assert.True(ingredient.NeedsReview);

        ingredient.Confirm();

        Assert.False(ingredient.NeedsReview);
    }

    [Fact]
    public void Base_units_are_g_ml_and_piece() =>
        Assert.Equal(["g", "ml", "piece"], Units.BaseUnits.Select(CatalogCode.Of));

    [Theory]
    [InlineData("vegetablesAndFruit", IngredientCategory.VegetablesAndFruit)]
    [InlineData("DairyAndEggs", IngredientCategory.DairyAndEggs)]
    [InlineData(" other ", IngredientCategory.Other)]
    public void Category_codes_parse(string code, IngredientCategory category)
    {
        Assert.True(CatalogCode.TryParse<IngredientCategory>(code, out var parsed));
        Assert.Equal(category, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("8")]
    [InlineData("sweets")]
    [InlineData("VegetablesAndFruit,Other")]
    public void Unknown_and_numeric_category_codes_are_rejected(string? code) =>
        Assert.False(CatalogCode.TryParse<IngredientCategory>(code, out _));

    [Fact]
    public void Unit_codes_are_short_symbols() =>
        Assert.Equal(
            ["g", "kg", "ml", "l", "piece", "clove", "tbsp", "tsp"],
            Units.Ordered.Take(8).Select(CatalogCode.Of));

    [Fact]
    public void Categories_keep_their_fixed_order_and_default_to_other()
    {
        Assert.Equal(IngredientCategory.VegetablesAndFruit, IngredientCategories.Ordered[0]);
        Assert.Equal(IngredientCategory.Other, IngredientCategories.Ordered[^1]);
        Assert.Equal(IngredientCategory.Other, IngredientCategories.Default);
    }
}
