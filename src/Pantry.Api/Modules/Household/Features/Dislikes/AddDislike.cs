using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.Dislikes;

/// <summary>
/// Adds a dislike of a catalog ingredient. An unknown ingredient is a validation error; an ingredient that already has a
/// dislike answers 409 with that dislike's id, so the SPA can change it instead.
/// </summary>
public static class AddDislike
{
    public static RouteHandlerBuilder MapAddDislike(this IEndpointRouteBuilder dislikes) => dislikes.MapPost("/", HandleAsync);

    private static async Task<Results<Created<DislikeResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        AddDislikeRequest request, HouseholdDbContext db, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        if (DislikeValidation.Validate(request.Kind, request.Reason, out var kind, out var reason) is { } invalid)
        {
            return invalid;
        }

        var ingredient = request.IngredientId is { } ingredientId ? await catalog.GetIngredientAsync(ingredientId, cancellationToken) : null;
        if (ingredient is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["IngredientId"] = ["Choose an ingredient from the catalog."],
            });
        }

        if (await db.Dislikes.AsNoTracking().SingleOrDefaultAsync(d => d.IngredientId == ingredient.Id, cancellationToken) is { } existing)
        {
            return AlreadyDisliked(existing.Id);
        }

        var dislike = Dislike.Create(ingredient.Id, kind, reason);
        db.Dislikes.Add(dislike);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (HouseholdDbContext.IsDuplicateDislike(exception))
        {
            db.Entry(dislike).State = EntityState.Detached;
            var winner = await db.Dislikes.AsNoTracking().SingleAsync(d => d.IngredientId == ingredient.Id, cancellationToken);
            return AlreadyDisliked(winner.Id);
        }

        return TypedResults.Created($"/api/household/dislikes/{dislike.Id}", DislikeResponse.From(dislike, ingredient));
    }

    private static ProblemHttpResult AlreadyDisliked(Guid dislikeId) => TypedResults.Problem(
        title: "The ingredient is already disliked.",
        detail: "Change the existing dislike instead.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["dislikeId"] = dislikeId });
}
