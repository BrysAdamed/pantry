using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>One recipe, archived or not, with ingredient names from the catalog.</summary>
public static class GetRecipe
{
    public static RouteHandlerBuilder MapGetRecipe(this IEndpointRouteBuilder recipes) => recipes.MapGet("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<RecipeResponse>, NotFound>> HandleAsync(
        Guid id, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsNoTracking().AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return recipe is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(await RecipeResponse.FromAsync(recipe, catalog, household, cancellationToken));
    }
}
