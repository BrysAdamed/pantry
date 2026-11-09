using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog;

internal sealed class CatalogModuleContract(CatalogDbContext db) : ICatalogModule
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

    public async Task<BaseQuantity?> ConvertToBaseUnitAsync(
        Guid ingredientId, decimal quantity, Unit unit, CancellationToken cancellationToken = default) =>
        (await GetIngredientAsync(ingredientId, cancellationToken))?.ConvertToBaseUnit(quantity, unit);

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
