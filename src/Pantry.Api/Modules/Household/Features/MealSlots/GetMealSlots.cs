using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

/// <summary>All meal slots in planner order, disabled ones included.</summary>
public static class GetMealSlots
{
    public static RouteHandlerBuilder MapGetMealSlots(this IEndpointRouteBuilder slots) => slots.MapGet("/", HandleAsync);

    private static async Task<IReadOnlyList<MealSlotResponse>> HandleAsync(HouseholdDbContext db, CancellationToken cancellationToken) =>
        await db.MealSlots.AsNoTracking()
            .OrderBy(s => s.Position)
            .Select(s => new MealSlotResponse(s.Id, s.Name, s.Position, s.IsEnabled))
            .ToListAsync(cancellationToken);
}
