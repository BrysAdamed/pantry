namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// What the household thinks of a recipe: its <i>make again</i> flag and the average of the ratings of its history
/// entries. A <i>favourite</i> is derived, never set.
/// </summary>
public sealed record RecipeRating(bool MakeAgain, decimal? AverageRating, int RatingCount)
{
    public const decimal FavouriteMinAverage = 4m;

    private const int AverageDecimals = 2;

    public static RecipeRating Of(bool makeAgain, int ratingSum, int ratingCount) => new(
        makeAgain,
        ratingCount == 0 ? null : (decimal)ratingSum / ratingCount,
        ratingCount);

    /// <summary>Make again is yes and the average rating is at least <see cref="FavouriteMinAverage"/>.</summary>
    public bool IsFavourite => MakeAgain && AverageRating >= FavouriteMinAverage;

    /// <summary>The average for display, rounded to two decimal places; <see cref="IsFavourite"/> uses the exact one.</summary>
    public decimal? RoundedAverage => AverageRating is { } average ? Math.Round(average, AverageDecimals, MidpointRounding.AwayFromZero) : null;
}
