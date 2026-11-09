using System.Data.Common;
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

    /// <summary>Make again, average rating, rating count and favourite of the recipes among <paramref name="recipeIds"/>, archived or not. Unknown ids are left out.</summary>
    Task<IReadOnlyList<RecipeRatingSummary>> GetRatingsAsync(IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);

    /// <summary>The favourites that are not archived, in name order: what the tray and agents offer first.</summary>
    Task<IReadOnlyList<RecipeSummary>> GetFavouritesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The cooldown in effect and the <i>available from</i> date of the recipes among <paramref name="recipeIds"/>,
    /// archived or not. Unknown ids are left out. Planning a recipe before its date is <i>too soon</i>, a soft hint only.
    /// </summary>
    Task<IReadOnlyList<RecipeCooldownSummary>> GetCooldownsAsync(IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The household dislikes among the ingredients of the recipes among <paramref name="recipeIds"/>: per recipe its
    /// <i>never</i> ingredients first, then its <i>avoid</i> ones. The planner asks for confirmation before a person places
    /// a recipe with a <i>never</i> ingredient. Recipes without any are left out.
    /// </summary>
    Task<IReadOnlyList<RecipeDislikeWarning>> GetDislikeWarningsAsync(
        IReadOnlyCollection<Guid> recipeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes <paramref name="image"/> (JPEG, PNG or WebP, at most <c>Recipes:Photos:MaxBytes</c>) the photo of a recipe,
    /// archived or not, in place of the one it had; for an agent that attaches the picture from the recipe's source page.
    /// The picture is resized and loses its metadata (ADR 0025).
    /// </summary>
    Task<RecipePhotoResult> SetPhotoAsync(Guid recipeId, Stream image, CancellationToken cancellationToken = default);

    /// <summary>Removes the photo of a recipe, if it has one; false when the recipe does not exist.</summary>
    Task<bool> RemovePhotoAsync(Guid recipeId, CancellationToken cancellationToken = default);
}

/// <summary>What became of a recipe photo upload.</summary>
public enum RecipePhotoResult
{
    Saved,
    RecipeNotFound,

    /// <summary>Larger than <c>Recipes:Photos:MaxBytes</c>.</summary>
    TooLarge,

    /// <summary>Not a JPEG, PNG or WebP picture that could be read, or one with too many pixels.</summary>
    NotAPhoto,
}

/// <summary>A household dislike of an ingredient of a recipe; only a <see cref="DislikeKind.Never"/> one can have a reason.</summary>
public sealed record RecipeDislikeWarning(Guid RecipeId, Guid IngredientId, DislikeKind Kind, string? Reason);

/// <summary>A plan entry that became eaten, as the Planner reports it. A leftover eats a dish cooked earlier.</summary>
public sealed record EatenPlanEntry(Guid PlanEntryId, Guid RecipeId, DateOnly Date, Guid MealSlotId, bool IsLeftover);

/// <summary>
/// One time the household ate a recipe: the plan entry it came from, on <c>Date</c> in the meal slot <c>MealSlotId</c>.
/// <c>Rating</c> is 1 to 5 stars, null while not rated.
/// </summary>
public sealed record RecipeHistoryEntry(Guid Id, Guid RecipeId, DateOnly Date, Guid MealSlotId, Guid PlanEntryId, int? Rating);

/// <summary>
/// What the household thinks of a recipe. <c>AverageRating</c> is over its rated history entries (null when none);
/// a favourite has make again and an average of at least 4.
/// </summary>
public sealed record RecipeRatingSummary(Guid RecipeId, bool MakeAgain, decimal? AverageRating, int RatingCount, bool IsFavourite);

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
    bool IsArchived,
    bool HasPhoto);

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

/// <summary>
/// When a recipe should come back. <c>CooldownDays</c> is the recipe's own cooldown or else the household's
/// (<c>IsHouseholdDefault</c>), 0 for none. <c>AvailableFrom</c> is last eaten + cooldown, the first day that is not too
/// soon; null when the recipe was never eaten, which means it is available now.
/// </summary>
public sealed record RecipeCooldownSummary(Guid RecipeId, int CooldownDays, bool IsHouseholdDefault, DateOnly? LastEaten, DateOnly? AvailableFrom)
{
    /// <summary>Whether planning the recipe on <paramref name="date"/> is too soon.</summary>
    public bool IsTooSoonOn(DateOnly date) => Domain.RecipeCooldown.IsTooSoon(AvailableFrom, date);
}
