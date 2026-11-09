using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

/// <summary>All planning rules in order, disabled ones included.</summary>
public static class GetPlanningRules
{
    public static RouteHandlerBuilder MapGetPlanningRules(this IEndpointRouteBuilder rules) => rules.MapGet("/", HandleAsync);

    private static async Task<IReadOnlyList<PlanningRuleResponse>> HandleAsync(HouseholdDbContext db, CancellationToken cancellationToken) =>
        await db.PlanningRules.AsNoTracking()
            .OrderBy(r => r.Position)
            .Select(r => new PlanningRuleResponse(r.Id, r.Text, r.Position, r.IsEnabled))
            .ToListAsync(cancellationToken);
}
