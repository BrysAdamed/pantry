using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

/// <summary>Sets the order in one transaction. The request lists every planning rule id exactly once, disabled ones included.</summary>
public static class ReorderPlanningRules
{
    public static RouteHandlerBuilder MapReorderPlanningRules(this IEndpointRouteBuilder rules) => rules.MapPut("/order", HandleAsync);

    private static async Task<Results<NoContent, ValidationProblem>> HandleAsync(
        ReorderPlanningRulesRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var rules = await db.PlanningRules.ToDictionaryAsync(r => r.Id, cancellationToken);
        var ids = request.PlanningRuleIds ?? [];
        if (ids.Count != rules.Count || ids.Distinct().Count() != ids.Count || !ids.All(rules.ContainsKey))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.PlanningRuleIds)] = ["List every planning rule exactly once."],
            });
        }

        for (var position = 0; position < ids.Count; position++)
        {
            rules[ids[position]].Position = position;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record ReorderPlanningRulesRequest(IReadOnlyList<Guid>? PlanningRuleIds);
