using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;

namespace Pantry.Api.Modules.Recipes;

/// <summary>
/// In-process contract of the Recipes module. The Planner, Shopping and the MCP server read recipes through it and keep
/// only their ids. Reads by id include archived recipes, because plan entries and history still show them.
/// </summary>
public interface IRecipesModule
{
    /// <summary>The recipe, archived or not; null when it does not exist.</summary>
    Task<RecipeSummary?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The recipes that exist among <paramref name="ids"/>, archived or not, in name order. Unknown ids are left out.</summary>
    Task<IReadOnlyList<RecipeSummary>> GetRecipesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ingredient lines of the recipes among <paramref name="recipeIds"/> for one serving, in each recipe's order.
    /// Multiply by the household serving of a plan entry, then sum with <see cref="ICatalogModule.SumQuantitiesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RecipeIngredientPerServing>> GetIngredientsPerServingAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The household dislikes among the ingredients of the recipes among <paramref name="recipeIds"/>: per recipe its
    /// <i>never</i> ingredients first, then its <i>avoid</i> ones. The planner asks for confirmation before a person places
    /// a recipe with a <i>never</i> ingredient. Recipes without any are left out.
    /// </summary>
    Task<IReadOnlyList<RecipeDislikeWarning>> GetDislikeWarningsAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);
}

/// <summary>A household dislike of an ingredient of a recipe; only a <see cref="DislikeKind.Never"/> one can have a reason.</summary>
public sealed record RecipeDislikeWarning(Guid RecipeId, Guid IngredientId, DislikeKind Kind, string? Reason);

/// <summary>
/// A recipe as other modules see it. <c>Servings</c> is what its quantities were entered for; <c>CooldownDays</c> is the
/// recipe's own override, null to use the household default; <c>Nutrition</c> is per serving.
/// </summary>
public sealed record RecipeSummary(
    Guid Id,
    string Name,
    int Servings,
    int? PrepTimeMinutes,
    IReadOnlyList<string> Tags,
    Nutrition Nutrition,
    bool MakeAgain,
    int? CooldownDays,
    bool IsArchived);

/// <summary>
/// One ingredient line of a recipe for one serving. <c>Quantity</c> and <c>Unit</c> are both null for an ingredient
/// added <i>to taste</i>.
/// </summary>
public sealed record RecipeIngredientPerServing(
    Guid RecipeId,
    Guid IngredientId,
    decimal? Quantity,
    Unit? Unit,
    string? Note,
    string? Section)
{
    /// <summary>The line for <paramref name="servings"/> (a household serving), ready for <see cref="ICatalogModule.SumQuantitiesAsync"/>.</summary>
    public QuantityLine ToQuantityLine(decimal servings) => new(IngredientId, Quantity * servings, Unit);
}
