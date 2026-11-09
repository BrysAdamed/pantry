using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

/// <summary>Removes a planning rule for good and closes the gap in the order.</summary>
public static class DeletePlanningRule
{
    public static RouteHandlerBuilder MapDeletePlanningRule(this IEndpointRouteBuilder rules) => rules.MapDelete("/{id:guid}", HandleAsync);

    private static async Task<Results<NoContent, NotFound>> HandleAsync(Guid id, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockHouseholdAsync(cancellationToken);
        var rule = await db.PlanningRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return TypedResults.NotFound();
        }

        db.PlanningRules.Remove(rule);
        await db.PlanningRules.Where(r => r.Position > rule.Position)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Position, r => r.Position - 1), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
