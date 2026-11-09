using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

public static class RenameMealSlot
{
    public static RouteHandlerBuilder MapRenameMealSlot(this IEndpointRouteBuilder slots) => slots.MapPut("/{id:guid}/name", HandleAsync);

    private static async Task<Results<Ok<MealSlotResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, MealSlotNameRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (MealSlotValidation.Validate(request, out var name) is { } problem)
        {
            return problem;
        }

        var slot = await db.MealSlots.FindAsync([id], cancellationToken);
        if (slot is null)
        {
            return TypedResults.NotFound();
        }

        slot.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(MealSlotResponse.From(slot));
    }
}
