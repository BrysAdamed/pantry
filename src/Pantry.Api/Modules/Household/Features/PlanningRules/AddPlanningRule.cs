using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Domain;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

/// <summary>Adds an enabled planning rule at the end of the order.</summary>
public static class AddPlanningRule
{
    public static RouteHandlerBuilder MapAddPlanningRule(this IEndpointRouteBuilder rules) => rules.MapPost("/", HandleAsync);

    private static async Task<Results<Created<PlanningRuleResponse>, ValidationProblem>> HandleAsync(
        PlanningRuleTextRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (PlanningRuleValidation.Validate(request, out var text) is { } problem)
        {
            return problem;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var count = await db.PlanningRules.CountAsync(cancellationToken);
        var rule = new PlanningRule { Text = text, Position = count };
        db.PlanningRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TypedResults.Created($"/api/household/planning-rules/{rule.Id}", PlanningRuleResponse.From(rule));
    }
}
