using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Features.Ingredients;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Duplicates;

/// <summary>
/// Pairs of ingredients with similar names or aliases, most similar first, without the pairs marked as not duplicates
/// (ADR 0022). <c>ingredientId</c> limits the pairs to one ingredient.
/// </summary>
public static class GetPossibleDuplicates
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 500;

    private const string PairsSql =
        """
        SELECT a.ingredient_id, b.ingredient_id, max(similarity(a.key, b.key))
        FROM catalog.ingredient_names a
        JOIN catalog.ingredient_names b ON b.key % a.key AND a.ingredient_id < b.ingredient_id
        WHERE (@ingredient_id IS NULL OR a.ingredient_id = @ingredient_id OR b.ingredient_id = @ingredient_id)
          AND NOT EXISTS (
              SELECT 1 FROM catalog.not_duplicate_pairs d
              WHERE d.first_ingredient_id = a.ingredient_id AND d.second_ingredient_id = b.ingredient_id)
        GROUP BY a.ingredient_id, b.ingredient_id
        ORDER BY 3 DESC, 1, 2
        LIMIT @limit
        """;

    public static RouteHandlerBuilder MapGetPossibleDuplicates(this IEndpointRouteBuilder catalog) =>
        catalog.MapGet("/possible-duplicates", HandleAsync);

    internal static async Task<IReadOnlyList<(Ingredient First, Ingredient Second, double Similarity)>> FindAsync(
        CatalogDbContext db, double threshold, Guid? ingredientId, int limit, CancellationToken cancellationToken)
    {
        var pairs = await QueryPairsAsync(db, threshold, ingredientId, limit, cancellationToken);
        var ids = pairs.SelectMany(p => new[] { p.FirstId, p.SecondId }).Distinct().ToList();
        var ingredients = await db.Ingredients.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return pairs
            .Where(p => ingredients.ContainsKey(p.FirstId) && ingredients.ContainsKey(p.SecondId))
            .Select(p => (ingredients[p.FirstId], ingredients[p.SecondId], Math.Round(p.Similarity, 3)))
            .ToList();
    }

    private static async Task<Results<Ok<PossibleDuplicatesResponse>, ValidationProblem>> HandleAsync(
        CatalogDbContext db,
        IOptions<PossibleDuplicateSettings> settings,
        CancellationToken cancellationToken,
        Guid? ingredientId = null,
        int limit = DefaultLimit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(limit)] = [$"Limit must be between 1 and {MaxLimit}."],
            });
        }

        var pairs = await FindAsync(db, settings.Value.Threshold, ingredientId, limit, cancellationToken);
        return TypedResults.Ok(new PossibleDuplicatesResponse(pairs
            .Select(p => new PossibleDuplicateResponse(IngredientResponse.From(p.First), IngredientResponse.From(p.Second), p.Similarity))
            .ToList()));
    }

    /// <summary>
    /// <c>%</c> compares with <c>pg_trgm.similarity_threshold</c> and can use the trigram index, so the threshold is set
    /// for this transaction only.
    /// </summary>
    private static async Task<List<(Guid FirstId, Guid SecondId, double Similarity)>> QueryPairsAsync(
        CatalogDbContext db, double threshold, Guid? ingredientId, int limit, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var npgsqlTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();

        await using (var setThreshold = new NpgsqlCommand("SELECT set_config('pg_trgm.similarity_threshold', @threshold, true)", connection, npgsqlTransaction))
        {
            setThreshold.Parameters.AddWithValue("threshold", threshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await setThreshold.ExecuteNonQueryAsync(cancellationToken);
        }

        var pairs = new List<(Guid, Guid, double)>();
        await using (var query = new NpgsqlCommand(PairsSql, connection, npgsqlTransaction))
        {
            query.Parameters.Add(new NpgsqlParameter("ingredient_id", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = (object?)ingredientId ?? DBNull.Value });
            query.Parameters.AddWithValue("limit", limit);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pairs.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetFloat(2)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return pairs;
    }
}

public sealed record PossibleDuplicateResponse(IngredientResponse First, IngredientResponse Second, double Similarity);

public sealed record PossibleDuplicatesResponse(IReadOnlyList<PossibleDuplicateResponse> Items);
