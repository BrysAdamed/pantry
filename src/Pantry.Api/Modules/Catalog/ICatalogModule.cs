namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// In-process contract of the Catalog module. Recipes, Household (dislikes), Shopping and the MCP server read
/// ingredients through it and keep only their ids.
/// </summary>
public interface ICatalogModule
{
    /// <summary>The ingredient, or null when it does not exist (deleted or merged away).</summary>
    Task<CatalogIngredient?> GetIngredientAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The ingredients that exist among <paramref name="ids"/>, in catalog order. Unknown ids are left out.</summary>
    Task<IReadOnlyList<CatalogIngredient>> GetIngredientsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ingredient that has <paramref name="name"/> as its name or an alias, ignoring letter case, diacritics and
    /// extra whitespace; null when there is none.
    /// </summary>
    Task<CatalogIngredient?> FindIngredientAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ingredient that has <paramref name="name"/> as its name or an alias (as <see cref="FindIngredientAsync"/>), or
    /// a new one created and committed right away: base unit guessed from <paramref name="unitUsed"/>, category
    /// <c>other</c>, needs review. Parallel calls with the same new name create one ingredient. Throws
    /// <see cref="ArgumentException"/> for a name that is empty or longer than 100 characters.
    /// </summary>
    Task<ResolvedIngredient> ResolveIngredientAsync(string name, Unit? unitUsed, CancellationToken cancellationToken = default);

    /// <summary>Clears <c>needs review</c> of the ingredient; null when it does not exist.</summary>
    Task<CatalogIngredient?> ConfirmIngredientAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pairs of ingredients with similar names or aliases, most similar first, leaving out pairs marked as not
    /// duplicates. <paramref name="ingredientId"/> limits them to the pairs of one ingredient.
    /// </summary>
    Task<IReadOnlyList<PossibleDuplicate>> GetPossibleDuplicatesAsync(
        Guid? ingredientId = null, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks two different ingredients as not duplicates, so the pair is no longer suggested; false when either does
    /// not exist.
    /// </summary>
    Task<bool> MarkNotDuplicatesAsync(Guid oneIngredientId, Guid otherIngredientId, CancellationToken cancellationToken = default);
}

/// <summary>Two ingredients that may be the same; <c>Similarity</c> is from 0 to 1 (1 for identical names).</summary>
public sealed record PossibleDuplicate(CatalogIngredient First, CatalogIngredient Second, double Similarity);

/// <summary>The result of <see cref="ICatalogModule.ResolveIngredientAsync"/>; <c>Created</c> is true for a new ingredient.</summary>
public sealed record ResolvedIngredient(CatalogIngredient Ingredient, bool Created);

/// <summary>An ingredient as other modules see it. <c>Aliases</c> are sorted; <c>BaseUnit</c> is <c>g</c>, <c>ml</c> or <c>piece</c>.</summary>
public sealed record CatalogIngredient(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    Unit BaseUnit,
    IngredientCategory Category,
    bool IsStaple,
    bool NeedsReview);
