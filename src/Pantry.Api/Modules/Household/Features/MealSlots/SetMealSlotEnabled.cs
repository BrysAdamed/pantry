using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

/// <summary>Enables or disables a meal slot. The last enabled slot cannot be disabled, so the planner always has a row.</summary>
public static class SetMealSlotEnabled
{
    public static RouteHandlerBuilder MapSetMealSlotEnabled(this IEndpointRouteBuilder slots) => slots.MapPut("/{id:guid}/enabled", HandleAsync);

    private static async Task<Results<Ok<MealSlotResponse>, NotFound, ProblemHttpResult>> HandleAsync(
        Guid id, SetMealSlotEnabledRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var slot = await db.MealSlots.FindAsync([id], cancellationToken);
        if (slot is null)
        {
            return TypedResults.NotFound();
        }

        if (!request.IsEnabled && !await db.MealSlots.AnyAsync(s => s.Id != id && s.IsEnabled, cancellationToken))
        {
            return TypedResults.Problem(
                title: "The last enabled meal slot cannot be disabled.",
                detail: "Enable another meal slot first.",
                statusCode: StatusCodes.Status409Conflict);
        }

        slot.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(MealSlotResponse.From(slot));
    }
}

public sealed record SetMealSlotEnabledRequest(bool IsEnabled);
