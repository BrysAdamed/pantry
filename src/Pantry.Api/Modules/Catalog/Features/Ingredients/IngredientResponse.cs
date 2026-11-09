using Pantry.Api.Modules.Catalog.Domain;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

public sealed record IngredientResponse(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    Unit BaseUnit,
    IngredientCategory Category,
    bool IsStaple,
    bool NeedsReview)
{
    public static IngredientResponse From(Ingredient ingredient) => new(
        ingredient.Id,
        ingredient.Name,
        ingredient.Aliases,
        ingredient.BaseUnit,
        ingredient.Category,
        ingredient.IsStaple,
        ingredient.NeedsReview);
}
