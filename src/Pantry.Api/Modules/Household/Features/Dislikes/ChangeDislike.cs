using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Dislikes;

/// <summary>Changes the kind and reason of a dislike; changing to avoid drops the reason.</summary>
public static class ChangeDislike
{
    public static RouteHandlerBuilder MapChangeDislike(this IEndpointRouteBuilder dislikes) => dislikes.MapPut("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<DislikeResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, ChangeDislikeRequest request, HouseholdDbContext db, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        if (DislikeValidation.Validate(request.Kind, request.Reason, out var kind, out var reason) is { } invalid)
        {
            return invalid;
        }

        var dislike = await db.Dislikes.FindAsync([id], cancellationToken);
        if (dislike is null || await catalog.GetIngredientAsync(dislike.IngredientId, cancellationToken) is not { } ingredient)
        {
            return TypedResults.NotFound();
        }

        dislike.Change(kind, reason);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(DislikeResponse.From(dislike, ingredient));
    }
}
