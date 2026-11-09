using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Tags;

/// <summary>
/// Tags for autocomplete: those used by recipes that are not archived, most used first. <c>search</c> matches part of a
/// tag, compared the way tags are stored (trimmed, lower case).
/// </summary>
public static class GetTags
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static RouteHandlerBuilder MapGetTags(this IEndpointRouteBuilder recipes) => recipes.MapGet("/tags", HandleAsync);

    private static async Task<Results<Ok<IReadOnlyList<TagUsage>>, ValidationProblem>> HandleAsync(
        RecipesDbContext db, CancellationToken cancellationToken, string? search = null, int limit = DefaultLimit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(limit)] = [$"Limit must be between 1 and {MaxLimit}."],
            });
        }

        var tags = db.Recipes.AsNoTracking().Where(r => r.ArchivedAt == null).SelectMany(r => r.Tags).Select(t => t.Value);
        var term = RecipeTag.Normalize(search ?? string.Empty);
        if (term.Length > 0)
        {
            tags = tags.Where(t => t.Contains(term));
        }

        var usage = await tags
            .GroupBy(t => t)
            .Select(g => new { Tag = g.Key, Recipes = g.Count() })
            .OrderByDescending(u => u.Recipes).ThenBy(u => u.Tag)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<TagUsage>>(usage.Select(u => new TagUsage(u.Tag, u.Recipes)).ToList());
    }
}

/// <summary>A tag and the number of recipes (not archived) that have it.</summary>
public sealed record TagUsage(string Tag, int Recipes);
