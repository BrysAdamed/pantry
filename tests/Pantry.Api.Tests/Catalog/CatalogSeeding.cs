using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;
using Pantry.Api.Tests.Infrastructure;
using Pantry.Api.Tests.Infrastructure.TestModule;

namespace Pantry.Api.Tests.Catalog;

/// <summary>Writes ingredients and test module references straight to the database, for states the API cannot set up.</summary>
public static class CatalogSeeding
{
    public static async Task<Guid> SeedIngredientAsync(
        this PantryApiFactory factory,
        string name,
        string[] aliases,
        Unit baseUnit,
        bool needsReview = false,
        bool isStaple = false,
        IngredientCategory category = IngredientCategory.Other)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var ingredient = Ingredient.Create(name, aliases, baseUnit, category, isStaple, needsReview);
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return ingredient.Id;
    }

    public static async Task AddTestReferencesAsync(this PantryApiFactory factory, Guid ingredientId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        db.IngredientReferences.AddRange(Enumerable.Range(0, count).Select(_ => new TestIngredientReference { IngredientId = ingredientId }));
        await db.SaveChangesAsync();
    }

    public static async Task<int> CountTestReferencesAsync(this PantryApiFactory factory, Guid ingredientId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        return await db.IngredientReferences.CountAsync(r => r.IngredientId == ingredientId);
    }
}
