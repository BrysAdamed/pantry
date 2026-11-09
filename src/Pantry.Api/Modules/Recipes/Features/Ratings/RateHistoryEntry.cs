using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Ratings;

/// <summary>
/// Rates a history entry with 1 to 5 stars (add or change), or removes its rating. A rating that answers
/// <c>makeAgain</c> sets the recipe's flag, so the latest answer wins; leaving it out keeps the flag. Archived recipes
/// can be rated too.
/// </summary>
public static class RateHistoryEntry
{
    public static IEndpointRouteBuilder MapRateHistoryEntry(this IEndpointRouteBuilder recipes)
    {
        recipes.MapPut("/history/{entryId:guid}/rating", RateAsync);
        recipes.MapDelete("/history/{entryId:guid}/rating", RemoveAsync);
        return recipes;
    }

    private static async Task<Results<Ok<RatingResponse>, NotFound, ValidationProblem>> RateAsync(
        Guid entryId, RatingRequest request, RecipesDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (request.Stars is not { } stars || !HistoryEntry.IsValidRating(stars))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["stars"] = [$"A rating is {HistoryEntry.MinStars} to {HistoryEntry.MaxStars} stars."],
            });
        }

        var entry = await db.HistoryEntries.SingleOrDefaultAsync(h => h.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        var recipe = await db.Recipes.AsSplitQuery().SingleAsync(r => r.Id == entry.RecipeId, cancellationToken);
        var now = time.GetUtcNow();
        entry.Rate(stars, now);
        if (request.MakeAgain is { } makeAgain)
        {
            recipe.SetMakeAgain(makeAgain, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await RatingResponse.FromAsync(entry, recipe, db, cancellationToken));
    }

    private static async Task<Results<Ok<RatingResponse>, NotFound>> RemoveAsync(
        Guid entryId, RecipesDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.HistoryEntries.SingleOrDefaultAsync(h => h.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        entry.RemoveRating();
        await db.SaveChangesAsync(cancellationToken);
        var recipe = await db.Recipes.AsNoTracking().AsSplitQuery().SingleAsync(r => r.Id == entry.RecipeId, cancellationToken);
        return TypedResults.Ok(await RatingResponse.FromAsync(entry, recipe, db, cancellationToken));
    }
}

/// <summary>Stars from 1 to 5; <c>MakeAgain</c> is the answer to <i>make again?</i>, left out when not answered.</summary>
public sealed record RatingRequest(int? Stars, bool? MakeAgain);

/// <summary>The rated (or unrated) history entry and what the household now thinks of its recipe.</summary>
public sealed record RatingResponse(RecipeHistoryEntry Entry, RecipeRatingSummary Recipe)
{
    public static async Task<RatingResponse> FromAsync(HistoryEntry entry, Recipe recipe, RecipesDbContext db, CancellationToken cancellationToken) => new(
        new RecipeHistoryEntry(entry.Id, entry.RecipeId, entry.Date, entry.MealSlotId, entry.PlanEntryId, entry.Rating),
        RecipeRatings.ToSummary(recipe.Id, await RecipeRatings.LoadAsync(db, recipe, cancellationToken)));
}
