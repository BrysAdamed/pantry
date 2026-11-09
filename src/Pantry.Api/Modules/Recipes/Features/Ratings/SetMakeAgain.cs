using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Ratings;

/// <summary>Changes <i>make again</i> on a recipe, archived or not, at any time. <i>No</i> means agents stop proposing it.</summary>
public static class SetMakeAgain
{
    public static RouteHandlerBuilder MapSetMakeAgain(this IEndpointRouteBuilder recipes) => recipes.MapPut("/{id:guid}/make-again", HandleAsync);

    private static async Task<Results<Ok<RecipeRatingSummary>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, MakeAgainRequest request, RecipesDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (request.MakeAgain is not { } makeAgain)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["makeAgain"] = ["Make again is required."] });
        }

        var recipe = await db.Recipes.AsSplitQuery().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recipe is null)
        {
            return TypedResults.NotFound();
        }

        recipe.SetMakeAgain(makeAgain, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(RecipeRatings.ToSummary(recipe.Id, await RecipeRatings.LoadAsync(db, recipe, cancellationToken)));
    }
}

public sealed record MakeAgainRequest(bool? MakeAgain);
