using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// Where an ingredient is used: the reference count of each module that uses it, in module order. An empty list means
/// nothing uses it and it can be deleted.
/// </summary>
public static class GetIngredientReferences
{
    public static RouteHandlerBuilder MapGetIngredientReferences(this IEndpointRouteBuilder ingredients) =>
        ingredients.MapGet("/{id:guid}/references", HandleAsync);

    private static async Task<Results<Ok<IngredientReferencesResponse>, NotFound>> HandleAsync(
        Guid id, CatalogDbContext db, IngredientReferences references, CancellationToken cancellationToken)
    {
        if (!await db.Ingredients.AnyAsync(i => i.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var counts = await references.CountAsync(id, cancellationToken);
        return TypedResults.Ok(new IngredientReferencesResponse(counts.Where(c => c.Count > 0).ToList()));
    }
}

public sealed record IngredientReferencesResponse(IReadOnlyList<ModuleReferenceCount> References);
