using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Features.Duplicates;
using Pantry.Api.Modules.Catalog.Features.Ingredients;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog;

internal sealed class CatalogModuleContract(CatalogDbContext db, IOptions<PossibleDuplicateSettings> duplicateSettings) : ICatalogModule
{
    public async Task<CatalogIngredient?> GetIngredientAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ingredient = await db.Ingredients.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return ingredient is null ? null : ToContract(ingredient);
    }

    public async Task<IReadOnlyList<CatalogIngredient>> GetIngredientsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var ingredients = await db.Ingredients.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .OrderBy(i => i.NameKey)
            .ToListAsync(cancellationToken);
        return ingredients.Select(ToContract).ToList();
    }

    public async Task<CatalogIngredient?> FindIngredientAsync(string name, CancellationToken cancellationToken = default)
    {
        var key = IngredientName.KeyOf(name);
        var ingredient = await db.Ingredients.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Names.Any(n => n.Key == key), cancellationToken);
        return ingredient is null ? null : ToContract(ingredient);
    }

    public async Task<IReadOnlyList<Guid>> SearchIngredientIdsAsync(string search, CancellationToken cancellationToken = default)
    {
        var key = IngredientName.KeyOf(search);
        if (key.Length == 0)
        {
            return [];
        }

        return await db.Ingredients.AsNoTracking()
            .Where(i => i.Names.Any(n => n.Key.Contains(key)))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<BaseQuantity?> ConvertToBaseUnitAsync(
        Guid ingredientId, decimal quantity, Unit unit, CancellationToken cancellationToken = default) =>
        (await GetIngredientAsync(ingredientId, cancellationToken))?.ConvertToBaseUnit(quantity, unit);

    public async Task<QuantityTotals> SumQuantitiesAsync(IReadOnlyCollection<QuantityLine> lines, CancellationToken cancellationToken = default)
    {
        var ingredients = await GetIngredientsAsync(lines.Select(l => l.IngredientId).Distinct().ToList(), cancellationToken);
        return QuantityCalculator.Sum(lines, ingredients);
    }

    public async Task<ResolvedIngredient> ResolveIngredientAsync(string name, Unit? unitUsed, CancellationToken cancellationToken = default)
    {
        var cleanName = IngredientName.Clean(name);
        if (!Ingredient.IsValidName(cleanName))
        {
            throw new ArgumentException($"An ingredient name must have 1 to {Ingredient.NameMaxLength} characters.", nameof(name));
        }

        var (ingredient, created) = await ResolveIngredient.ResolveAsync(db, cleanName, unitUsed, cancellationToken);
        return new ResolvedIngredient(ToContract(ingredient), created);
    }

    public async Task<CatalogIngredient?> ConfirmIngredientAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ConfirmIngredient.ConfirmAsync(db, id, cancellationToken) is { } ingredient ? ToContract(ingredient) : null;

    public async Task<IReadOnlyList<PossibleDuplicate>> GetPossibleDuplicatesAsync(
        Guid? ingredientId = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        var pairs = await GetPossibleDuplicates.FindAsync(db, duplicateSettings.Value.Threshold, ingredientId, limit, cancellationToken);
        return pairs.Select(p => new PossibleDuplicate(ToContract(p.First), ToContract(p.Second), p.Similarity)).ToList();
    }

    public Task<bool> MarkNotDuplicatesAsync(Guid oneIngredientId, Guid otherIngredientId, CancellationToken cancellationToken = default) =>
        MarkNotDuplicates.MarkAsync(db, oneIngredientId, otherIngredientId, cancellationToken);

    private static CatalogIngredient ToContract(Ingredient ingredient) => new(
        ingredient.Id,
        ingredient.Name,
        ingredient.Aliases,
        ingredient.BaseUnit,
        ingredient.Category,
        ingredient.IsStaple,
        ingredient.NeedsReview,
        ingredient.ConversionAmounts());
}
