using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

public static class EditPlanningRule
{
    public static RouteHandlerBuilder MapEditPlanningRule(this IEndpointRouteBuilder rules) => rules.MapPut("/{id:guid}/text", HandleAsync);

    private static async Task<Results<Ok<PlanningRuleResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid id, PlanningRuleTextRequest request, HouseholdDbContext db, CancellationToken cancellationToken)
    {
        if (PlanningRuleValidation.Validate(request, out var text) is { } problem)
        {
            return problem;
        }

        var rule = await db.PlanningRules.FindAsync([id], cancellationToken);
        if (rule is null)
        {
            return TypedResults.NotFound();
        }

        rule.Text = text;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(PlanningRuleResponse.From(rule));
    }
}
