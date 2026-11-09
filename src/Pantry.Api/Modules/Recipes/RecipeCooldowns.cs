using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

/// <summary>Works out <see cref="RecipeCooldown"/> of recipes from their own cooldown, the household's and their history.</summary>
internal static class RecipeCooldowns
{
    public static async Task<IReadOnlyDictionary<Guid, RecipeCooldown>> LoadAsync(
        RecipesDbContext db,
        IHouseholdModule household,
        IReadOnlyCollection<(Guid Id, int? CooldownDays)> recipes,
        CancellationToken cancellationToken)
    {
        var householdDays = await household.GetCooldownDaysAsync(cancellationToken);
        var ids = recipes.Select(r => r.Id).ToList();
        var lastEaten = await db.HistoryEntries
            .Where(h => ids.Contains(h.RecipeId))
            .GroupBy(h => h.RecipeId)
            .Select(g => new { RecipeId = g.Key, Date = g.Max(h => h.Date) })
            .ToDictionaryAsync(x => x.RecipeId, x => x.Date, cancellationToken);
        return recipes.ToDictionary(
            r => r.Id,
            r => RecipeCooldown.Of(r.CooldownDays, householdDays, lastEaten.TryGetValue(r.Id, out var date) ? date : null));
    }

    public static Task<IReadOnlyDictionary<Guid, RecipeCooldown>> LoadAsync(
        RecipesDbContext db, IHouseholdModule household, IEnumerable<Recipe> recipes, CancellationToken cancellationToken) =>
        LoadAsync(db, household, recipes.Select(r => (r.Id, r.CooldownDays)).ToList(), cancellationToken);

    public static async Task<RecipeCooldown> LoadAsync(
        RecipesDbContext db, IHouseholdModule household, Recipe recipe, CancellationToken cancellationToken) =>
        (await LoadAsync(db, household, [recipe], cancellationToken))[recipe.Id];
}
