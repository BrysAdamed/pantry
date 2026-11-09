using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.History;

/// <summary>When the household ate a recipe, archived or not: its history entries, newest first.</summary>
public static class GetRecipeHistory
{
    public static RouteHandlerBuilder MapGetRecipeHistory(this IEndpointRouteBuilder recipes) => recipes.MapGet("/{id:guid}/history", HandleAsync);

    private static async Task<Results<Ok<IReadOnlyList<RecipeHistoryEntry>>, NotFound>> HandleAsync(
        Guid id, RecipesDbContext db, IRecipesModule recipes, CancellationToken cancellationToken)
    {
        if (!await db.Recipes.AnyAsync(r => r.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await recipes.GetHistoryAsync(id, cancellationToken));
    }
}
