using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Tests.Recipes;

public sealed class RecipeRatingTests
{
    [Theory]
    [InlineData(true, 8, 2, true)]
    [InlineData(true, 5, 1, true)]
    [InlineData(true, 11, 3, false)]
    [InlineData(true, 3, 1, false)]
    [InlineData(false, 10, 2, false)]
    [InlineData(true, 0, 0, false)]
    public void Favourite_needs_make_again_and_an_average_of_at_least_four(bool makeAgain, int sum, int count, bool favourite)
    {
        var rating = RecipeRating.Of(makeAgain, sum, count);

        Assert.Equal(favourite, rating.IsFavourite);
    }

    [Fact]
    public void Favourite_uses_the_exact_average_not_the_rounded_one()
    {
        var rating = RecipeRating.Of(makeAgain: true, ratingSum: 799, ratingCount: 200);

        Assert.Equal(4.00m, rating.RoundedAverage);
        Assert.False(rating.IsFavourite);
    }

    [Fact]
    public void Recipe_never_rated_has_no_average()
    {
        var rating = RecipeRating.Of(makeAgain: true, ratingSum: 0, ratingCount: 0);

        Assert.Null(rating.AverageRating);
        Assert.Equal(0, rating.RatingCount);
    }

    [Fact]
    public void Average_is_rounded_to_two_decimal_places()
    {
        var rating = RecipeRating.Of(makeAgain: true, ratingSum: 14, ratingCount: 3);

        Assert.Equal(4.67m, rating.RoundedAverage);
        Assert.True(rating.IsFavourite);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rating_outside_one_to_five_stars_is_not_valid(int stars)
    {
        Assert.False(HistoryEntry.IsValidRating(stars));
    }
}
