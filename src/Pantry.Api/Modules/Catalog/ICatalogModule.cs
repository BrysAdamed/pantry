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
}

/// <summary>An ingredient as other modules see it. <c>Aliases</c> are sorted; <c>BaseUnit</c> is <c>g</c>, <c>ml</c> or <c>piece</c>.</summary>
public sealed record CatalogIngredient(
    Guid Id,
    string Name,
    IReadOnlyList<string> Aliases,
    Unit BaseUnit,
    IngredientCategory Category,
    bool IsStaple,
    bool NeedsReview);
