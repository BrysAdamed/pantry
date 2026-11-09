using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// A page of the catalog in name order, optionally filtered by category, staple flag and needs review. <c>search</c>
/// matches part of a name or an alias, ignoring letter case and diacritics; ingredients with a name or alias that
/// starts with it come first.
/// </summary>
public static class GetIngredients
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static RouteHandlerBuilder MapGetIngredients(this IEndpointRouteBuilder ingredients) => ingredients.MapGet("/", HandleAsync);

    private static async Task<Results<Ok<IngredientsPage>, ValidationProblem>> HandleAsync(
        CatalogDbContext db,
        CancellationToken cancellationToken,
        string? search = null,
        string? category = null,
        bool? staple = null,
        bool? needsReview = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var errors = new Dictionary<string, string[]>();
        IngredientCategory? categoryFilter = null;
        if (category is not null)
        {
            if (CatalogCode.TryParse<IngredientCategory>(category, out var parsed))
            {
                categoryFilter = parsed;
            }
            else
            {
                errors[nameof(category)] = [$"Category must be one of: {string.Join(", ", IngredientCategories.Ordered.Select(CatalogCode.Of))}."];
            }
        }

        if (page < 1)
        {
            errors[nameof(page)] = ["Page starts at 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors[nameof(pageSize)] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = db.Ingredients.AsNoTracking();
        if (categoryFilter is { } c)
        {
            query = query.Where(i => i.Category == c);
        }

        if (staple is { } isStaple)
        {
            query = query.Where(i => i.IsStaple == isStaple);
        }

        if (needsReview is { } review)
        {
            query = query.Where(i => i.NeedsReview == review);
        }

        var key = IngredientName.KeyOf(search ?? string.Empty);
        IOrderedQueryable<Ingredient> ordered;
        if (key.Length > 0)
        {
            query = query.Where(i => i.Names.Any(n => n.Key.Contains(key)));
            ordered = query.OrderBy(i => i.Names.Any(n => n.Key.StartsWith(key)) ? 0 : 1).ThenBy(i => i.NameKey);
        }
        else
        {
            ordered = query.OrderBy(i => i.NameKey);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ordered.ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new IngredientsPage(items.Select(IngredientResponse.From).ToList(), total, page, pageSize));
    }
}

public sealed record IngredientsPage(IReadOnlyList<IngredientResponse> Items, int Total, int Page, int PageSize);
