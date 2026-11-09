using System.Data.Common;

namespace Pantry.Api.Modules.Catalog;

/// <summary>
/// Implemented by every module that stores ingredient ids (Household dislikes, Recipes, Shopping) and registered with
/// <c>services.AddScoped&lt;IIngredientReferenceHandler, ...&gt;()</c>. The catalog asks the handlers before deleting an
/// ingredient and lets them move their references when one ingredient is merged into another (ADR 0019).
/// </summary>
public interface IIngredientReferenceHandler
{
    /// <summary>The owning module, as shown in a merge preview or a refused delete (for example <c>recipes</c>).</summary>
    string Module { get; }

    /// <summary>How many of the module's records point at the ingredient.</summary>
    Task<int> CountReferencesAsync(Guid ingredientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points every reference to <paramref name="sourceId"/> at <paramref name="targetId"/> inside the catalog's
    /// transaction (ADR 0015), resolving clashes by the module's own rules. Throwing rolls the whole merge back.
    /// </summary>
    Task MoveReferencesAsync(Guid sourceId, Guid targetId, DbTransaction transaction, CancellationToken cancellationToken = default);
}
