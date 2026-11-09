using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>Adds a recipe. Ingredient lines given by name are resolved in the catalog, creating ingredients that need review.</summary>
public static class CreateRecipe
{
    public static RouteHandlerBuilder MapCreateRecipe(this IEndpointRouteBuilder recipes) => recipes.MapPost("/", HandleAsync);

    private static async Task<Results<Created<RecipeResponse>, ValidationProblem>> HandleAsync(
        RecipeRequest request, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, TimeProvider time, CancellationToken cancellationToken)
    {
        var (content, errors) = await RecipeInput.ToContentAsync(request, currentMakeAgain: true, catalog, cancellationToken);
        if (content is null)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var recipe = Recipe.Create(content, time.GetUtcNow());
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/recipes/{recipe.Id}", await RecipeResponse.FromAsync(recipe, catalog, household, cancellationToken));
    }
}
