using System.Data.Common;

namespace Pantry.Api.Modules.Catalog;

/// <summary>How many records of one module point at an ingredient.</summary>
public sealed record ModuleReferenceCount(string Module, int Count);

/// <summary>Asks every registered <see cref="IIngredientReferenceHandler"/> in a fixed order (by module).</summary>
internal sealed class IngredientReferences(IEnumerable<IIngredientReferenceHandler> handlers)
{
    private readonly IReadOnlyList<IIngredientReferenceHandler> _handlers =
        handlers.OrderBy(h => h.Module, StringComparer.Ordinal).ToList();

    public async Task<IReadOnlyList<ModuleReferenceCount>> CountAsync(Guid ingredientId, CancellationToken cancellationToken)
    {
        var counts = new List<ModuleReferenceCount>(_handlers.Count);
        foreach (var handler in _handlers)
        {
            counts.Add(new ModuleReferenceCount(handler.Module, await handler.CountReferencesAsync(ingredientId, cancellationToken)));
        }

        return counts;
    }

    public async Task MoveAsync(Guid sourceId, Guid targetId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            await handler.MoveReferencesAsync(sourceId, targetId, transaction, cancellationToken);
        }
    }
}
