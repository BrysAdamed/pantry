using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Ratings;

/// <summary>
/// The <i>to rate</i> list: history entries without a rating from the last <c>days</c> days, today included (default
/// from <see cref="ToRateSettings"/>), newest first, archived recipes included. An entry left for later stays here
/// until it is rated or too old.
/// </summary>
public static class GetToRate
{
    public static RouteHandlerBuilder MapGetToRate(this IEndpointRouteBuilder recipes) => recipes.MapGet("/history/to-rate", HandleAsync);

    private static async Task<Results<Ok<IReadOnlyList<ToRateItem>>, ValidationProblem>> HandleAsync(
        RecipesDbContext db, IOptions<ToRateSettings> settings, TimeProvider time, CancellationToken cancellationToken, int? days = null)
    {
        var window = days ?? settings.Value.Days;
        if (window is < 1 or > ToRateSettings.MaxDays)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(days)] = [$"Days must be between 1 and {ToRateSettings.MaxDays}."],
            });
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var from = today.AddDays(1 - window);
        IReadOnlyList<ToRateItem> items = await db.HistoryEntries
            .Where(h => h.Rating == null && h.Date >= from && h.Date <= today)
            .Join(db.Recipes, h => h.RecipeId, r => r.Id, (h, r) => new { History = h, Recipe = r })
            .OrderByDescending(x => x.History.Date).ThenByDescending(x => x.History.RecordedAt).ThenByDescending(x => x.History.Id)
            .Select(x => new ToRateItem(
                x.History.Id,
                x.History.RecipeId,
                x.Recipe.Name,
                x.Recipe.ArchivedAt != null,
                x.History.Date,
                x.History.MealSlotId,
                x.History.PlanEntryId))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(items);
    }
}

/// <summary>A history entry waiting for a rating, with the name of its recipe.</summary>
public sealed record ToRateItem(
    Guid Id,
    Guid RecipeId,
    string RecipeName,
    bool RecipeIsArchived,
    DateOnly Date,
    Guid MealSlotId,
    Guid PlanEntryId);
