using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

/// <summary>Adds an enabled meal slot at the end of the planner order.</summary>
public static class AddMealSlot
{
    public static RouteHandlerBuilder MapAddMealSlot(this IEndpointRouteBuilder slots) => slots.MapPost("/", HandleAsync);

    private static async Task<Results<Created<MealSlotResponse>, ValidationProblem>> HandleAsync(
        MealSlotNameRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (MealSlotValidation.Validate(request, out var name) is { } problem)
        {
            return problem;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var count = await db.MealSlots.CountAsync(cancellationToken);
        var slot = new MealSlot { Name = name, Position = count };
        db.MealSlots.Add(slot);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created($"/api/household/meal-slots/{slot.Id}", MealSlotResponse.From(slot));
    }
}
