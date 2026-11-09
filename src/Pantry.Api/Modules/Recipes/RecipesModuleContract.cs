using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

internal sealed class RecipesModuleContract(RecipesDbContext db, DbContextOptions<RecipesDbContext> options, TimeProvider time) : IRecipesModule
{
    public async Task<RecipeSummary?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recipe = await db.Recipes.AsNoTracking().AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return recipe is null ? null : ToSummary(recipe);
    }

    public async Task<IReadOnlyList<RecipeSummary>> GetRecipesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var recipes = await db.Recipes.AsNoTracking().AsSplitQuery()
            .Where(r => ids.Contains(r.Id))
            .OrderBy(r => r.Name.ToLower()).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
        return recipes.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<RecipeIngredientPerServing>> GetIngredientsPerServingAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default)
    {
        var recipes = await db.Recipes.AsNoTracking().AsSplitQuery()
            .Where(r => recipeIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        return recipes
            .SelectMany(recipe => recipe.OrderedLines().Select(line => new RecipeIngredientPerServing(
                recipe.Id, line.IngredientId, line.QuantityPerServing, line.Unit, line.Note, line.Section)))
            .ToList();
    }

    public async Task RecordEatenAsync(EatenPlanEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);
        var existing = await joined.HistoryEntries.SingleOrDefaultAsync(h => h.PlanEntryId == entry.PlanEntryId, cancellationToken);

        if (entry.IsLeftover)
        {
            if (existing is not null)
            {
                joined.HistoryEntries.Remove(existing);
                await joined.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (!await joined.Recipes.AnyAsync(r => r.Id == entry.RecipeId, cancellationToken))
        {
            throw new ArgumentException($"The recipe {entry.RecipeId} does not exist.", nameof(entry));
        }

        if (existing is null)
        {
            joined.HistoryEntries.Add(HistoryEntry.Create(entry, time.GetUtcNow()));
        }
        else
        {
            existing.Update(entry, time.GetUtcNow());
        }

        await joined.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordUneatenAsync(Guid planEntryId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = await JoinAsync(transaction, cancellationToken);
        await joined.HistoryEntries.Where(h => h.PlanEntryId == planEntryId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastEatenAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default) =>
        await db.HistoryEntries
            .Where(h => recipeIds.Contains(h.RecipeId))
            .GroupBy(h => h.RecipeId)
            .Select(g => new { RecipeId = g.Key, Date = g.Max(h => h.Date) })
            .ToDictionaryAsync(x => x.RecipeId, x => x.Date, cancellationToken);

    public async Task<IReadOnlyList<RecipeHistoryEntry>> GetHistoryAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
        await db.HistoryEntries
            .Where(h => h.RecipeId == recipeId)
            .OrderByDescending(h => h.Date).ThenByDescending(h => h.RecordedAt).ThenByDescending(h => h.Id)
            .Select(h => new RecipeHistoryEntry(h.Id, h.RecipeId, h.Date, h.MealSlotId, h.PlanEntryId))
            .ToListAsync(cancellationToken);

    internal static RecipeSummary ToSummary(Recipe recipe) => new(
        recipe.Id,
        recipe.Name,
        recipe.Servings,
        recipe.PrepTimeMinutes,
        recipe.TagValues(),
        recipe.Nutrition,
        recipe.MakeAgain,
        recipe.CooldownDays,
        recipe.IsArchived);

    private async Task<RecipesDbContext> JoinAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        var joined = new RecipesDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);
        return joined;
    }
}
