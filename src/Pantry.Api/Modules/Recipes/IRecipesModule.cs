using System.Data.Common;
using Pantry.Api.Modules.Catalog;

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
    /// Records that a plan entry was eaten, inside the caller's transaction (ADR 0015). Idempotent per plan entry: a
    /// second call keeps one history entry and takes the recipe, date and meal slot it is given. A leftover is not
    /// history, so recording one removes the plan entry's history entry if it has one.
    /// </summary>
    /// <exception cref="ArgumentException">The recipe does not exist.</exception>
    Task RecordEatenAsync(EatenPlanEntry entry, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a plan entry is no longer eaten (or was deleted), inside the caller's transaction (ADR 0015): its
    /// history entry goes, together with its rating. Does nothing when it has none.
    /// </summary>
    Task RecordUneatenAsync(Guid planEntryId, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>The date each recipe among <paramref name="recipeIds"/> was last eaten. Recipes never eaten are left out.</summary>
    Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastEatenAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);

    /// <summary>The history entries of a recipe, newest first. Empty when it was never eaten or does not exist.</summary>
    Task<IReadOnlyList<RecipeHistoryEntry>> GetHistoryAsync(Guid recipeId, CancellationToken cancellationToken = default);
}

/// <summary>A plan entry that became eaten, as the Planner reports it. A leftover eats a dish cooked earlier.</summary>
public sealed record EatenPlanEntry(Guid PlanEntryId, Guid RecipeId, DateOnly Date, Guid MealSlotId, bool IsLeftover);

/// <summary>One time the household ate a recipe: the plan entry it came from, on <c>Date</c> in the meal slot <c>MealSlotId</c>.</summary>
public sealed record RecipeHistoryEntry(Guid Id, Guid RecipeId, DateOnly Date, Guid MealSlotId, Guid PlanEntryId);

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
