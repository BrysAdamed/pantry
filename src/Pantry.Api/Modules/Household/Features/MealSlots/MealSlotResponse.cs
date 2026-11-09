using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Features.MealSlots;

public sealed record MealSlotResponse(Guid Id, string Name, int Position, bool IsEnabled)
{
    public static MealSlotResponse From(MealSlot slot) => new(slot.Id, slot.Name, slot.Position, slot.IsEnabled);
}

public sealed record MealSlotNameRequest(string? Name);

internal static class MealSlotValidation
{
    public static ValidationProblem? Validate(MealSlotNameRequest request, out string name)
    {
        name = request.Name?.Trim() ?? string.Empty;
        return MealSlot.IsValidName(name)
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(MealSlotNameRequest.Name)] = [$"Name must have 1 to {MealSlot.NameMaxLength} characters."],
            });
    }
}
