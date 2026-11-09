using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// Merges a source ingredient into the target (the one that stays): the source's names become aliases of the target,
/// every module moves its references in the same transaction (ADR 0015), the source is deleted and the target is
/// confirmed. The preview shows the same plan without changing anything.
/// </summary>
public static class MergeIngredients
{
    public static IEndpointRouteBuilder MapMergeIngredients(this IEndpointRouteBuilder ingredients)
    {
        ingredients.MapGet("/{id:guid}/merge-preview", PreviewAsync);
        ingredients.MapPost("/{id:guid}/merge", MergeAsync);
        return ingredients;
    }

    private static async Task<Results<Ok<MergePreviewResponse>, NotFound, ValidationProblem>> PreviewAsync(
        Guid id, Guid? sourceId, CatalogDbContext db, IngredientReferences references, CancellationToken cancellationToken)
    {
        if (ValidateSource(id, sourceId) is { } invalid)
        {
            return invalid;
        }

        var ingredients = await db.Ingredients.AsNoTracking()
            .Where(i => i.Id == id || i.Id == sourceId)
            .ToListAsync(cancellationToken);
        if (ingredients.SingleOrDefault(i => i.Id == id) is not { } target
            || ingredients.SingleOrDefault(i => i.Id == sourceId) is not { } source)
        {
            return TypedResults.NotFound();
        }

        var merge = IngredientMerge.Plan(target, source);
        var sourceCounts = await references.CountAsync(source.Id, cancellationToken);
        var targetCounts = await references.CountAsync(target.Id, cancellationToken);
        return TypedResults.Ok(new MergePreviewResponse(
            IngredientResponse.From(target),
            IngredientResponse.From(source),
            merge.AliasesToAdd,
            merge.AliasCountAfter(target),
            merge.ExceedsAliasLimit(target),
            merge.SameBaseUnit,
            sourceCounts.Zip(targetCounts, (s, t) => new MergeReferences(s.Module, s.Count, t.Count)).ToList()));
    }

    private static async Task<Results<Ok<IngredientResponse>, NotFound, ValidationProblem, ProblemHttpResult>> MergeAsync(
        Guid id, MergeIngredientsRequest request, CatalogDbContext db, IngredientReferences references, CancellationToken cancellationToken)
    {
        if (ValidateSource(id, request.SourceId) is { } invalid)
        {
            return invalid;
        }

        var sourceId = request.SourceId!.Value;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockIngredientsAsync([id, sourceId], cancellationToken);
        var ingredients = await db.Ingredients.Where(i => i.Id == id || i.Id == sourceId).ToListAsync(cancellationToken);
        if (ingredients.SingleOrDefault(i => i.Id == id) is not { } target
            || ingredients.SingleOrDefault(i => i.Id == sourceId) is not { } source)
        {
            return TypedResults.NotFound();
        }

        var merge = IngredientMerge.Plan(target, source);
        if (merge.ExceedsAliasLimit(target))
        {
            return TypedResults.Problem(
                title: "Too many aliases after the merge.",
                detail: $"The ingredient would have {merge.AliasCountAfter(target)} aliases; at most {Ingredient.MaxAliases} are allowed. Remove some aliases first.",
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?> { ["aliasCountAfter"] = merge.AliasCountAfter(target) });
        }

        await references.MoveAsync(source.Id, target.Id, transaction.GetDbTransaction(), cancellationToken);
        db.Ingredients.Remove(source);
        await db.SaveChangesAsync(cancellationToken);
        target.Absorb(merge);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok(IngredientResponse.From(target));
    }

    private static ValidationProblem? ValidateSource(Guid targetId, Guid? sourceId) => sourceId switch
    {
        null => TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["SourceId"] = ["Choose the ingredient to merge."] }),
        { } same when same == targetId => TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { ["SourceId"] = ["An ingredient cannot be merged into itself."] }),
        _ => null,
    };
}

public sealed record MergeIngredientsRequest(Guid? SourceId);

/// <summary>
/// The plan of a merge: names the target gains, the alias count after it, whether both share a base unit, and the
/// references each module holds to the source (moving to the target) and to the target.
/// </summary>
public sealed record MergePreviewResponse(
    IngredientResponse Target,
    IngredientResponse Source,
    IReadOnlyList<string> AliasesToAdd,
    int AliasCountAfter,
    bool ExceedsAliasLimit,
    bool SameBaseUnit,
    IReadOnlyList<MergeReferences> References);

public sealed record MergeReferences(string Module, int Source, int Target);
