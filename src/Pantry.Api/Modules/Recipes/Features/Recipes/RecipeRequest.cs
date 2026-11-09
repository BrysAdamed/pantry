using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>
/// A recipe as the editor or an agent sends it, for create and update. Quantities are for <c>Servings</c>.
/// <c>Lines</c> are the ingredient lines outside any section and come first; <c>Sections</c> follow in order.
/// <c>MakeAgain</c> left out is yes for a new recipe and unchanged on update.
/// </summary>
public sealed record RecipeRequest(
    string? Name,
    int? Servings,
    int? PrepTimeMinutes,
    string? SourceUrl,
    IReadOnlyList<string?>? Tags,
    IReadOnlyList<string?>? Steps,
    NutritionRequest? Nutrition,
    bool? MakeAgain,
    int? CooldownDays,
    IReadOnlyList<RecipeLineRequest?>? Lines,
    IReadOnlyList<RecipeSectionRequest?>? Sections);

public sealed record NutritionRequest(decimal? Kcal, decimal? Protein, decimal? Fat, decimal? Carbs);

public sealed record RecipeSectionRequest(string? Name, IReadOnlyList<RecipeLineRequest?>? Lines);

/// <summary>
/// An ingredient line: either <c>IngredientId</c> of a catalog ingredient or <c>IngredientName</c>, which is resolved
/// in the catalog and creates an ingredient that needs review when the name is new. <c>Quantity</c> and <c>Unit</c>
/// go together; leave both out for an ingredient added to taste.
/// </summary>
public sealed record RecipeLineRequest(Guid? IngredientId, string? IngredientName, decimal? Quantity, Unit? Unit, string? Note);
