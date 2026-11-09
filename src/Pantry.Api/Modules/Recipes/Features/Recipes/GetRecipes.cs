using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Text;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>
/// A page of the recipe library: recipes that are not archived, or with <c>archived=true</c> only the archived ones.
/// <c>search</c> matches part of the name, a tag or the name or an alias of an ingredient, ignoring letter case and
/// diacritics; every <c>tag</c> given must be on the recipe; <c>withoutNever</c> leaves out recipes with a <i>never</i>
/// ingredient. <c>sort</c> is a <see cref="RecipeSort"/> code, name order by default.
/// </summary>
public static class GetRecipes
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static RouteHandlerBuilder MapGetRecipes(this IEndpointRouteBuilder recipes) => recipes.MapGet("/", HandleAsync);

    private static async Task<Results<Ok<RecipesPage>, ValidationProblem>> HandleAsync(
        RecipesDbContext db,
        ICatalogModule catalog,
        IHouseholdModule household,
        CancellationToken cancellationToken,
        string? search = null,
        string[]? tag = null,
        bool withoutNever = false,
        bool archived = false,
        string? sort = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var errors = new Dictionary<string, string[]>();
        var order = RecipeSort.Name;
        if (sort is not null)
        {
            if (CatalogCode.TryParse<RecipeSort>(sort, out var parsed))
            {
                order = parsed.Value;
            }
            else
            {
                errors[nameof(sort)] = [$"Sort must be one of: {string.Join(", ", Enum.GetValues<RecipeSort>().Select(CatalogCode.Of))}."];
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

        var warnings = await DislikeWarnings.LoadAsync(household, cancellationToken);
        var query = db.Recipes.AsNoTracking().Where(r => (r.ArchivedAt != null) == archived);

        var key = TextKey.Of(search ?? string.Empty);
        if (key.Length > 0)
        {
            var ingredientIds = await catalog.SearchIngredientIdsAsync(key, cancellationToken);
            query = query.Where(r => r.SearchKey.Contains(key) || r.Lines.Any(l => ingredientIds.Contains(l.IngredientId)));
        }

        foreach (var wanted in (tag ?? []).Select(RecipeTag.Normalize).Where(t => t.Length > 0).Distinct())
        {
            query = query.Where(r => r.Tags.Any(t => t.Value == wanted));
        }

        if (withoutNever)
        {
            var neverIds = warnings.NeverIngredientIds();
            query = query.Where(r => !r.Lines.Any(l => neverIds.Contains(l.IngredientId)));
        }

        var total = await query.CountAsync(cancellationToken);
        var keyed = query.Select(r => new
        {
            r.Id,
            Name = r.Name.ToLower(),
            r.PrepTimeMinutes,
            AverageRating = db.HistoryEntries.Where(h => h.RecipeId == r.Id && h.Rating != null).Average(h => (double?)h.Rating),
            RatingCount = db.HistoryEntries.Count(h => h.RecipeId == r.Id && h.Rating != null),
            LastEaten = db.HistoryEntries.Where(h => h.RecipeId == r.Id).Max(h => (DateOnly?)h.Date),
        });
        var ordered = order switch
        {
            RecipeSort.Rating => keyed.OrderBy(k => k.AverageRating == null).ThenByDescending(k => k.AverageRating).ThenByDescending(k => k.RatingCount),
            RecipeSort.RecentlyEaten => keyed.OrderBy(k => k.LastEaten == null).ThenByDescending(k => k.LastEaten),
            RecipeSort.NotEatenLately => keyed.OrderBy(k => k.LastEaten != null).ThenBy(k => k.LastEaten),
            RecipeSort.PrepTime => keyed.OrderBy(k => k.PrepTimeMinutes == null).ThenBy(k => k.PrepTimeMinutes),
            _ => keyed.OrderBy(k => k.Name),
        };
        var ids = await ordered.ThenBy(k => k.Name).ThenBy(k => k.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(k => k.Id)
            .ToListAsync(cancellationToken);
        var loaded = await db.Recipes.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .AsSplitQuery()
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        var items = ids.Select(id => loaded[id]).ToList();

        var dislikes = items.ToDictionary(r => r.Id, warnings.Of);
        var dislikedIds = dislikes.Values.SelectMany(d => d).Select(d => d.IngredientId).Distinct().ToList();
        var names = (await catalog.GetIngredientsAsync(dislikedIds, cancellationToken)).ToDictionary(i => i.Id, i => i.Name);
        var ratings = await RecipeRatings.LoadAsync(db, items, cancellationToken);
        var cooldowns = await RecipeCooldowns.LoadAsync(db, household, items, cancellationToken);

        return TypedResults.Ok(new RecipesPage(
            items.Select(recipe => RecipeListItem.From(
                    recipe,
                    ratings[recipe.Id],
                    cooldowns[recipe.Id],
                    dislikes[recipe.Id].Select(d => RecipeDislikeResponse.From(d, names.GetValueOrDefault(d.IngredientId))).ToList()))
                .ToList(),
            total,
            page,
            pageSize));
    }
}

public sealed record RecipesPage(IReadOnlyList<RecipeListItem> Items, int Total, int Page, int PageSize);

/// <summary>
/// A recipe as the library lists it; <c>Nutrition</c> is per serving, <c>AverageRating</c> is null while no history entry
/// is rated, <c>Dislikes</c> are never first.
/// </summary>
public sealed record RecipeListItem(
    Guid Id,
    string Name,
    int Servings,
    int? PrepTimeMinutes,
    IReadOnlyList<string> Tags,
    Nutrition Nutrition,
    bool MakeAgain,
    decimal? AverageRating,
    int RatingCount,
    bool IsFavourite,
    int? CooldownDays,
    RecipeCooldown Cooldown,
    bool IsArchived,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RecipeDislikeResponse> Dislikes)
{
    public static RecipeListItem From(Recipe recipe, RecipeRating rating, RecipeCooldown cooldown, IReadOnlyList<RecipeDislikeResponse> dislikes) => new(
        recipe.Id,
        recipe.Name,
        recipe.Servings,
        recipe.PrepTimeMinutes,
        recipe.TagValues(),
        recipe.Nutrition,
        recipe.MakeAgain,
        rating.RoundedAverage,
        rating.RatingCount,
        rating.IsFavourite,
        recipe.CooldownDays,
        cooldown,
        recipe.IsArchived,
        recipe.UpdatedAt,
        dislikes);
}
