using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// Deletes an ingredient nothing uses. When a module still references it, answers 409 with the counts per module, so
/// the SPA can suggest a merge instead.
/// </summary>
public static class DeleteIngredient
{
    public static RouteHandlerBuilder MapDeleteIngredient(this IEndpointRouteBuilder ingredients) => ingredients.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id, CatalogDbContext db, IngredientReferences references, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockIngredientsAsync([id], cancellationToken);
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (ingredient is null)
        {
            return TypedResults.NotFound();
        }

        var used = (await references.CountAsync(id, cancellationToken)).Where(c => c.Count > 0).ToList();
        if (used.Count > 0)
        {
            return TypedResults.Problem(
                title: "The ingredient is in use.",
                detail: "Merge it into another ingredient instead, so its references move there.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["references"] = used });
        }

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
