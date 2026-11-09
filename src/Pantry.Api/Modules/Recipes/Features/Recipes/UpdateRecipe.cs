using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>Replaces everything editable on a recipe, archived or not. Leaving <c>makeAgain</c> out keeps it.</summary>
public static class UpdateRecipe
{
    public static RouteHandlerBuilder MapUpdateRecipe(this IEndpointRouteBuilder recipes) => recipes.MapPut("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<RecipeResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, RecipeRequest request, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, TimeProvider time, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        var (content, errors) = await RecipeInput.ToContentAsync(request, recipe.MakeAgain, catalog, cancellationToken);
        if (content is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        recipe.Update(content, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await RecipeResponse.FromAsync(recipe, db, catalog, household, cancellationToken));
    }
}
