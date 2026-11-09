using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Features.PlanningRules;

public sealed record PlanningRuleResponse(Guid Id, string Text, int Position, bool IsEnabled)
{
    public static PlanningRuleResponse From(PlanningRule rule) => new(rule.Id, rule.Text, rule.Position, rule.IsEnabled);
}

public sealed record PlanningRuleTextRequest(string? Text);

internal static class PlanningRuleValidation
{
    public static ValidationProblem? Validate(PlanningRuleTextRequest request, out string text)
    {
        text = request.Text?.Trim() ?? string.Empty;
        return PlanningRule.IsValidText(text)
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(PlanningRuleTextRequest.Text)] = [$"Text must have 1 to {PlanningRule.TextMaxLength} characters."],
            });
    }
}
