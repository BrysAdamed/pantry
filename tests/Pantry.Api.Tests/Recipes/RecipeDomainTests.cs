using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Tests.Recipes;

public sealed class RecipeDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Flour = Guid.CreateVersion7();
    private static readonly Guid Salt = Guid.CreateVersion7();

    [Theory]
    [InlineData(500, 4, 125)]
    [InlineData(1, 3, 0.333333)]
    [InlineData(2, 3, 0.666667)]
    [InlineData(0.5, 1, 0.5)]
    [InlineData(0.001, 100, 0.00001)]
    public void Per_serving_quantity_divides_by_the_servings_and_rounds_to_six_places(double quantity, int servings, double expected)
    {
        Assert.Equal((decimal)expected, RecipeQuantities.PerServing((decimal)quantity, servings));
    }

    [Fact]
    public void Per_serving_quantity_needs_at_least_one_serving()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecipeQuantities.PerServing(1, 0));
    }

    [Theory]
    [InlineData(0.001, true)]
    [InlineData(100000, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(0.0005, false)]
    [InlineData(100000.001, false)]
    public void Quantity_must_be_positive_bounded_and_have_at_most_three_decimals(double quantity, bool valid)
    {
        Assert.Equal(valid, RecipeQuantities.IsValidQuantity((decimal)quantity));
    }

    [Fact]
    public void Lines_keep_the_entered_quantity_and_the_quantity_per_serving()
    {
        // Act
        var recipe = Recipe.Create(Content(servings: 4, Line(Flour, 500, Unit.G), Line(Salt, null, null)), Now);

        // Assert
        var lines = recipe.OrderedLines();
        Assert.Equal(500, lines[0].Quantity);
        Assert.Equal(125, lines[0].QuantityPerServing);
        Assert.Equal(Unit.G, lines[0].Unit);
        Assert.True(lines[1].IsToTaste);
        Assert.Null(lines[1].QuantityPerServing);
        Assert.Null(lines[1].Unit);
    }

    [Fact]
    public void Update_recomputes_the_quantity_per_serving_for_the_new_servings()
    {
        // Arrange
        var recipe = Recipe.Create(Content(servings: 4, Line(Flour, 500, Unit.G)), Now);

        // Act
        recipe.Update(Content(servings: 2, Line(Flour, 500, Unit.G)), Now.AddHours(1));

        // Assert
        var line = Assert.Single(recipe.OrderedLines());
        Assert.Equal(500, line.Quantity);
        Assert.Equal(250, line.QuantityPerServing);
        Assert.Equal(Now.AddHours(1), recipe.UpdatedAt);
        Assert.Equal(Now, recipe.CreatedAt);
    }

    [Fact]
    public void Update_with_fewer_lines_and_steps_drops_the_rest()
    {
        // Arrange
        var recipe = Recipe.Create(Content(servings: 2, Line(Flour, 200, Unit.G), Line(Salt, 1, Unit.Tsp)) with { Steps = ["Mix.", "Bake."] }, Now);

        // Act
        recipe.Update(Content(servings: 2, Line(Salt, 2, Unit.Tsp)) with { Steps = ["Season."] }, Now);

        // Assert
        var line = Assert.Single(recipe.OrderedLines());
        Assert.Equal(Salt, line.IngredientId);
        Assert.Equal(1, line.QuantityPerServing);
        Assert.Equal(["Season."], recipe.StepTexts());
    }

    [Fact]
    public void Nutrition_is_rounded_to_one_decimal_place()
    {
        var recipe = Recipe.Create(Content(servings: 3) with { Nutrition = new Nutrition(213.3333m, 12.35m, null, 0m) }, Now);

        Assert.Equal(new Nutrition(213.3m, 12.4m, null, 0m), recipe.Nutrition);
    }

    [Fact]
    public void Archive_and_restore_are_idempotent()
    {
        // Arrange
        var recipe = Recipe.Create(Content(servings: 1), Now);

        // Act
        recipe.Archive(Now.AddDays(1));
        recipe.Archive(Now.AddDays(2));

        // Assert
        Assert.True(recipe.IsArchived);
        Assert.Equal(Now.AddDays(1), recipe.ArchivedAt);
        recipe.Restore(Now.AddDays(3));
        Assert.False(recipe.IsArchived);
        Assert.Null(recipe.ArchivedAt);
    }

    [Theory]
    [InlineData("  Obiad ", "obiad")]
    [InlineData("NA  2\tDNI", "na 2 dni")]
    [InlineData("ŚNIADANIE", "śniadanie")]
    public void Tags_are_trimmed_collapsed_and_lower_case(string tag, string expected)
    {
        Assert.Equal(expected, RecipeTag.Normalize(tag));
    }

    [Theory]
    [InlineData(new string?[] { }, true)]
    [InlineData(new string?[] { null, null, "Sos", "Sos", "Ciasto" }, true)]
    [InlineData(new string?[] { "Sos", "sos", "Ciasto" }, true)]
    [InlineData(new string?[] { "Sos", null }, false)]
    [InlineData(new string?[] { "Sos", "Ciasto", "Sos" }, false)]
    public void Sections_must_be_grouped_after_the_lines_outside_them(string?[] sections, bool grouped)
    {
        Assert.Equal(grouped, RecipeContent.AreSectionsGrouped(sections));
    }

    [Fact]
    public void Invalid_content_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Recipe.Create(Content(servings: 0), Now));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Content(servings: 1, new RecipeLine(null, Flour, null, Unit.G, null)), Now));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Content(servings: 1) with { Tags = ["Obiad"] }, Now));
        Assert.Throws<ArgumentException>(() => Recipe.Create(Content(servings: 1) with { Nutrition = new Nutrition(-1, null, null, null) }, Now));
    }

    private static RecipeLine Line(Guid ingredientId, decimal? quantity, Unit? unit) => new(null, ingredientId, quantity, unit, null);

    private static RecipeContent Content(int servings, params RecipeLine[] lines) =>
        new("Example dish", servings, null, null, [], [], Nutrition.None, true, null, lines);
}
