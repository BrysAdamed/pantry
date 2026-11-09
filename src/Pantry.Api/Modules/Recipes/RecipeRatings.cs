using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

/// <summary>Works out <see cref="RecipeRating"/> of recipes from their make again flag and the ratings of their history entries.</summary>
internal static class RecipeRatings
{
    public static async Task<IReadOnlyDictionary<Guid, RecipeRating>> LoadAsync(
        RecipesDbContext db, IReadOnlyCollection<(Guid Id, bool MakeAgain)> recipes, CancellationToken cancellationToken)
    {
        var ids = recipes.Select(r => r.Id).ToList();
        var stats = await db.HistoryEntries
            .Where(h => ids.Contains(h.RecipeId) && h.Rating != null)
            .GroupBy(h => h.RecipeId)
            .Select(g => new { RecipeId = g.Key, Sum = g.Sum(h => h.Rating!.Value), Count = g.Count() })
            .ToDictionaryAsync(s => s.RecipeId, cancellationToken);
        return recipes.ToDictionary(
            r => r.Id,
            r => stats.TryGetValue(r.Id, out var s) ? RecipeRating.Of(r.MakeAgain, s.Sum, s.Count) : RecipeRating.Of(r.MakeAgain, 0, 0));
    }

    public static Task<IReadOnlyDictionary<Guid, RecipeRating>> LoadAsync(
        RecipesDbContext db, IEnumerable<Recipe> recipes, CancellationToken cancellationToken) =>
        LoadAsync(db, recipes.Select(r => (r.Id, r.MakeAgain)).ToList(), cancellationToken);

    public static async Task<RecipeRating> LoadAsync(RecipesDbContext db, Recipe recipe, CancellationToken cancellationToken) =>
        (await LoadAsync(db, [recipe], cancellationToken))[recipe.Id];

    public static RecipeRatingSummary ToSummary(Guid recipeId, RecipeRating rating) =>
        new(recipeId, rating.MakeAgain, rating.RoundedAverage, rating.RatingCount, rating.IsFavourite);
}
