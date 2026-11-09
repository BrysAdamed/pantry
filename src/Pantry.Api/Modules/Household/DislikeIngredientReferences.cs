using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household.Persistence;

namespace Pantry.Api.Modules.Household;

/// <summary>
/// Dislikes as references to catalog ingredients (ADR 0020). On a merge the source's dislike moves to the target; when
/// both are disliked, one dislike stays with the stricter kind (never wins over avoid).
/// </summary>
internal sealed class DislikeIngredientReferences(HouseholdDbContext db, DbContextOptions<HouseholdDbContext> options)
    : IIngredientReferenceHandler
{
    public const string ModuleName = "household";

    public string Module => ModuleName;

    public Task<int> CountReferencesAsync(Guid ingredientId, CancellationToken cancellationToken = default) =>
        db.Dislikes.CountAsync(d => d.IngredientId == ingredientId, cancellationToken);

    public async Task MoveReferencesAsync(Guid sourceId, Guid targetId, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using var joined = new HouseholdDbContext(options);
        await joined.Database.JoinTransactionAsync(transaction, cancellationToken);
        var dislikes = await joined.Dislikes
            .Where(d => d.IngredientId == sourceId || d.IngredientId == targetId)
            .ToListAsync(cancellationToken);
        if (dislikes.SingleOrDefault(d => d.IngredientId == sourceId) is not { } source)
        {
            return;
        }

        if (dislikes.SingleOrDefault(d => d.IngredientId == targetId) is { } target)
        {
            target.Absorb(source);
            joined.Dislikes.Remove(source);
        }
        else
        {
            source.IngredientId = targetId;
        }

        await joined.SaveChangesAsync(cancellationToken);
    }
}
