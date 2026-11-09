using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Duplicates;

/// <summary>Marks two ingredients as not duplicates, so the pair is no longer suggested. Marking a pair again changes nothing.</summary>
public static class MarkNotDuplicates
{
    public static RouteHandlerBuilder MapMarkNotDuplicates(this IEndpointRouteBuilder catalog) =>
        catalog.MapPost("/not-duplicates", HandleAsync);

    /// <summary>False when either ingredient does not exist.</summary>
    internal static async Task<bool> MarkAsync(CatalogDbContext db, Guid oneIngredientId, Guid otherIngredientId, CancellationToken cancellationToken)
    {
        var pair = NotDuplicatePair.Of(oneIngredientId, otherIngredientId);
        var found = await db.Ingredients.CountAsync(i => i.Id == pair.FirstIngredientId || i.Id == pair.SecondIngredientId, cancellationToken);
        if (found < 2)
        {
            return false;
        }

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO catalog.not_duplicate_pairs (first_ingredient_id, second_ingredient_id)
                VALUES ({pair.FirstIngredientId}, {pair.SecondIngredientId})
                ON CONFLICT DO NOTHING
                """,
                cancellationToken);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return false;
        }
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> HandleAsync(
        MarkNotDuplicatesRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (request.FirstIngredientId is not { } first || request.SecondIngredientId is not { } second || first == second)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["IngredientIds"] = ["Two different ingredients are needed."],
            });
        }

        return await MarkAsync(db, first, second, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}

public sealed record MarkNotDuplicatesRequest(Guid? FirstIngredientId, Guid? SecondIngredientId);
