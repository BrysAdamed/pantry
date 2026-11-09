using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

/// <summary>Turns a planning rule on or off. A disabled rule is kept for later but agents do not read it.</summary>
public static class SetPlanningRuleEnabled
{
    public static RouteHandlerBuilder MapSetPlanningRuleEnabled(this IEndpointRouteBuilder rules) => rules.MapPut("/{id:guid}/enabled", HandleAsync);

    private static async Task<Results<Ok<PlanningRuleResponse>, NotFound>> HandleAsync(
        Guid id, SetPlanningRuleEnabledRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        var rule = await db.PlanningRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return TypedResults.NotFound();
        }

        rule.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(PlanningRuleResponse.From(rule));
    }
}

public sealed record SetPlanningRuleEnabledRequest(bool IsEnabled);
