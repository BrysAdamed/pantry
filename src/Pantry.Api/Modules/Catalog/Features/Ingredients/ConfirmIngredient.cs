using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>Clears <c>needs review</c>. Confirming an ingredient that does not need review changes nothing.</summary>
public static class ConfirmIngredient
{
    public static RouteHandlerBuilder MapConfirmIngredient(this IEndpointRouteBuilder ingredients) =>
        ingredients.MapPost("/{id:guid}/confirm", HandleAsync);

    internal static async Task<Ingredient?> ConfirmAsync(CatalogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (ingredient is null)
        {
            return null;
        }

        ingredient.Confirm();
        await db.SaveChangesAsync(cancellationToken);
        return ingredient;
    }

    private static async Task<Results<Ok<IngredientResponse>, NotFound>> HandleAsync(
        Guid id, CatalogDbContext db, CancellationToken cancellationToken) =>
        await ConfirmAsync(db, id, cancellationToken) is { } ingredient
            ? TypedResults.Ok(IngredientResponse.From(ingredient))
            : TypedResults.NotFound();
}
