using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

internal sealed class RecipesModuleContract(RecipesDbContext db) : IRecipesModule
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
}
