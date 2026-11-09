using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>
/// Archives a recipe, which hides it from the library, the tray and agents but keeps it for plan entries and history,
/// or restores it. Recipes are never deleted. Both are idempotent.
/// </summary>
public static class ArchiveRecipe
{
    public static IEndpointRouteBuilder MapArchiveRecipe(this IEndpointRouteBuilder recipes)
    {
        recipes.MapPost("/{id:guid}/archive", (Guid id, RecipesDbContext db, ICatalogModule catalog, TimeProvider time, CancellationToken cancellationToken) =>
            ChangeAsync(id, db, catalog, recipe => recipe.Archive(time.GetUtcNow()), cancellationToken));
        recipes.MapPost("/{id:guid}/restore", (Guid id, RecipesDbContext db, ICatalogModule catalog, TimeProvider time, CancellationToken cancellationToken) =>
            ChangeAsync(id, db, catalog, recipe => recipe.Restore(time.GetUtcNow()), cancellationToken));
        return recipes;
    }

    private static async Task<Results<Ok<RecipeResponse>, NotFound>> ChangeAsync(
        Guid id, RecipesDbContext db, ICatalogModule catalog, Action<Recipe> change, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        change(recipe);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await RecipeResponse.FromAsync(recipe, catalog, cancellationToken));
    }
}
