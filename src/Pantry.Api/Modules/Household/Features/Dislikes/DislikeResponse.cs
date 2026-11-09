using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Modules.Household.Features.Dislikes;

public sealed record DislikeResponse(Guid Id, Guid IngredientId, string IngredientName, DislikeKind Kind, string? Reason)
{
    public static DislikeResponse From(Dislike dislike, CatalogIngredient ingredient) =>
        new(dislike.Id, dislike.IngredientId, ingredient.Name, dislike.Kind, dislike.Reason);
}

public sealed record AddDislikeRequest(Guid? IngredientId, DislikeKind? Kind, string? Reason);

public sealed record ChangeDislikeRequest(DislikeKind? Kind, string? Reason);

internal static class DislikeValidation
{
    public static ValidationProblem? Validate(DislikeKind? kind, string? reason, out DislikeKind validKind, out string? validReason)
    {
        var errors = new Dictionary<string, string[]>();
        validKind = kind ?? default;
        validReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (kind is null)
        {
            errors["Kind"] = ["Choose never or avoid."];
        }

        if (!Dislike.IsValidReason(validReason))
        {
            errors["Reason"] = [$"A reason must have at most {Dislike.ReasonMaxLength} characters."];
        }
        else if (validReason is not null && kind == DislikeKind.Avoid)
        {
            errors["Reason"] = ["Only a never dislike has a reason."];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }
}
