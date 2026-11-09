using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

public sealed record IngredientResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    Unit BaseUnit,
    IngredientCategory Category,
    bool IsStaple,
    bool NeedsReview,
    IReadOnlyList<ConversionResponse> Conversions)
{
    public static IngredientResponse From(Ingredient ingredient) => new(
        ingredient.Id,
        ingredient.Name,
        ingredient.Aliases,
        ingredient.BaseUnit,
        ingredient.Category,
        ingredient.IsStaple,
        ingredient.NeedsReview,
        ingredient.Conversions.OrderBy(c => c.Unit).Select(c => new ConversionResponse(c.Unit, c.Amount)).ToList());
}

/// <summary>One <c>unit</c> is <c>amount</c> of the ingredient's base unit.</summary>
public sealed record ConversionResponse(Unit Unit, decimal Amount);

/// <summary>One <c>unit</c> is <c>amount</c> of the base unit the request ends up with.</summary>
public sealed record ConversionRequest(Unit? Unit, decimal? Amount);
