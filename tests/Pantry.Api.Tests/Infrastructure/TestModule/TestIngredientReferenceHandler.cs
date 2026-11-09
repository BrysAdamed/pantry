using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Tests.Infrastructure.TestModule;

/// <summary>Switch for <see cref="TestIngredientReferenceHandler"/>: when set, it moves its references and then fails.</summary>
public sealed class TestIngredientReferenceFailure
{
    public bool FailMoves { get; set; }
}

/// <summary>A module's reference handler as a real module writes it: references move inside the catalog's transaction.</summary>
public sealed class TestIngredientReferenceHandler(
    TestModuleDbContext db,
    DbContextOptions<TestModuleDbContext> options,
    TestIngredientReferenceFailure failure) : IIngredientReferenceHandler
{
    public const string ModuleName = "test";

    public string Module => ModuleName;

    public Task<int> CountReferencesAsync(Guid ingredientId, CancellationToken cancellationToken = default) =>
        db.IngredientReferences.CountAsync(r => r.IngredientId == ingredientId, cancellationToken);

    public async Task MoveReferencesAsync(Guid sourceId, Guid targetId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = new TestModuleDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);
        await joined.IngredientReferences.Where(r => r.IngredientId == sourceId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IngredientId, targetId), cancellationToken);
        if (failure.FailMoves)
        {
            throw new InvalidOperationException("The test module failed to move its references.");
        }
    }
}
