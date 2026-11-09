using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

/// <summary>
/// Recipe ingredient lines as references to catalog ingredients (ADR 0020), archived recipes included. On a merge the
/// source's lines point at the target; a recipe with lines for both keeps both lines.
/// </summary>
internal sealed class RecipeIngredientReferences(RecipesDbContext db, DbContextOptions<RecipesDbContext> options)
    : IIngredientReferenceHandler
{
    public const string ModuleName = "recipes";

    public string Module => ModuleName;

    public Task<int> CountReferencesAsync(Guid ingredientId, CancellationToken cancellationToken = default) =>
        db.Recipes.SelectMany(r => r.Lines).CountAsync(l => l.IngredientId == ingredientId, cancellationToken);

    public async Task MoveReferencesAsync(Guid sourceId, Guid targetId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = new RecipesDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);
        var recipes = await joined.Recipes
            .Where(r => r.Lines.Any(l => l.IngredientId == sourceId))
            .ToListAsync(cancellationToken);
        foreach (var recipe in recipes)
        {
            recipe.ReplaceIngredient(sourceId, targetId);
        }

        await joined.SaveChangesAsync(cancellationToken);
    }
}
