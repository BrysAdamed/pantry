using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

/// <summary>Sets the planner order in one transaction. The request lists every meal slot id exactly once, disabled ones included.</summary>
public static class ReorderMealSlots
{
    public static RouteHandlerBuilder MapReorderMealSlots(this IEndpointRouteBuilder slots) => slots.MapPut("/order", HandleAsync);

    private static async Task<Results<NoContent, ValidationProblem>> HandleAsync(
        ReorderMealSlotsRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var slots = await db.MealSlots.ToDictionaryAsync(s => s.Id, cancellationToken);
        var ids = request.MealSlotIds ?? [];
        if (ids.Count != slots.Count || ids.Distinct().Count() != ids.Count || !ids.All(slots.ContainsKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.MealSlotIds)] = ["List every meal slot exactly once."],
            });
        }

        for (var position = 0; position < ids.Count; position++)
        {
            slots[ids[position]].Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record ReorderMealSlotsRequest(IReadOnlyList<Guid>? MealSlotIds);
