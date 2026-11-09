using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

public static class GetIngredient
{
    public static RouteHandlerBuilder MapGetIngredient(this IEndpointRouteBuilder ingredients) => ingredients.MapGet("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<IngredientResponse>, NotFound>> HandleAsync(Guid id, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return ingredient is null ? TypedResults.NotFound() : TypedResults.Ok(IngredientResponse.From(ingredient));
    }
}
