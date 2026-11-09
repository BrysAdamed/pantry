using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Dislikes;

/// <summary>All dislikes with their ingredient names: never first, then avoid, each in catalog order.</summary>
public static class GetDislikes
{
    public static RouteHandlerBuilder MapGetDislikes(this IEndpointRouteBuilder dislikes) => dislikes.MapGet("/", HandleAsync);

    private static async Task<IReadOnlyList<DislikeResponse>> HandleAsync(
        HouseholdDbContext db, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        var dislikes = await db.Dislikes.AsNoTracking().ToListAsync(cancellationToken);
        var ingredients = await catalog.GetIngredientsAsync(dislikes.Select(d => d.IngredientId).ToList(), cancellationToken);
        var byIngredient = dislikes.ToDictionary(d => d.IngredientId);
        return ingredients
            .Select(ingredient => DislikeResponse.From(byIngredient[ingredient.Id], ingredient))
            .OrderBy(d => d.Kind)
            .ToList();
    }
}
