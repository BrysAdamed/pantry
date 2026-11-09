using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>A page of the recipe library in name order: recipes that are not archived, or with <c>archived=true</c> only the archived ones.</summary>
public static class GetRecipes
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static RouteHandlerBuilder MapGetRecipes(this IEndpointRouteBuilder recipes) => recipes.MapGet("/", HandleAsync);

    private static async Task<Results<Ok<RecipesPage>, ValidationProblem>> HandleAsync(
        RecipesDbContext db,
        CancellationToken cancellationToken,
        bool archived = false,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var errors = new Dictionary<string, string[]>();
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

        var query = db.Recipes.AsNoTracking().Where(r => (r.ArchivedAt != null) == archived);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.Name.ToLower()).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new RecipesPage(items.Select(RecipeListItem.From).ToList(), total, page, pageSize));
    }
}

public sealed record RecipesPage(IReadOnlyList<RecipeListItem> Items, int Total, int Page, int PageSize);

/// <summary>A recipe as the library lists it; <c>Nutrition</c> is per serving.</summary>
public sealed record RecipeListItem(
    Guid Id,
    string Name,
    int Servings,
    int? PrepTimeMinutes,
    IReadOnlyList<string> Tags,
    Nutrition Nutrition,
    bool MakeAgain,
    int? CooldownDays,
    bool IsArchived,
    DateTimeOffset UpdatedAt)
{
    public static RecipeListItem From(Recipe recipe) => new(
        recipe.Id,
        recipe.Name,
        recipe.Servings,
        recipe.PrepTimeMinutes,
        recipe.TagValues(),
        recipe.Nutrition,
        recipe.MakeAgain,
        recipe.CooldownDays,
        recipe.IsArchived,
        recipe.UpdatedAt);
}
