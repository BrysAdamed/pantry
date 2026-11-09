using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>
/// A recipe with everything the detail and the editor show. <c>Lines</c> are outside any section; <c>Sections</c> follow
/// in order. Each line has the quantity as entered for <c>Servings</c> and per one serving. <c>Dislikes</c> are the
/// household dislikes among its ingredients, never first.
/// </summary>
public sealed record RecipeResponse(
    Guid Id,
    string Name,
    int Servings,
    int? PrepTimeMinutes,
    string? SourceUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Steps,
    Nutrition Nutrition,
    bool MakeAgain,
    decimal? AverageRating,
    int RatingCount,
    bool IsFavourite,
    int? CooldownDays,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RecipeLineResponse> Lines,
    IReadOnlyList<RecipeSectionResponse> Sections,
    IReadOnlyList<RecipeDislikeResponse> Dislikes)
{
    public static async Task<RecipeResponse> FromAsync(
        Recipe recipe, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, CancellationToken cancellationToken)
    {
        var rating = await RecipeRatings.LoadAsync(db, recipe, cancellationToken);
        var lines = recipe.OrderedLines();
        var ingredients = (await catalog.GetIngredientsAsync(lines.Select(l => l.IngredientId).Distinct().ToList(), cancellationToken))
            .ToDictionary(i => i.Id);
        var dislikes = (await DislikeWarnings.LoadAsync(household, cancellationToken)).Of(recipe);

        RecipeLineResponse ToResponse(RecipeIngredientLine line) => new(
            line.IngredientId,
            ingredients.GetValueOrDefault(line.IngredientId)?.Name,
            ingredients.GetValueOrDefault(line.IngredientId)?.NeedsReview ?? false,
            line.Quantity,
            line.QuantityPerServing,
            line.Unit,
            line.Note);

        var sections = lines
            .Where(l => l.Section is not null)
            .GroupBy(l => l.Section!)
            .Select(section => new RecipeSectionResponse(section.Key, section.Select(ToResponse).ToList()))
            .ToList();

        return new RecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.Servings,
            recipe.PrepTimeMinutes,
            recipe.SourceUrl,
            recipe.TagValues(),
            recipe.StepTexts(),
            recipe.Nutrition,
            recipe.MakeAgain,
            rating.RoundedAverage,
            rating.RatingCount,
            rating.IsFavourite,
            recipe.CooldownDays,
            recipe.IsArchived,
            recipe.ArchivedAt,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            lines.Where(l => l.Section is null).Select(ToResponse).ToList(),
            sections,
            dislikes.Select(d => RecipeDislikeResponse.From(d, ingredients.GetValueOrDefault(d.IngredientId)?.Name)).ToList());
    }
}

/// <summary>
/// A household dislike a recipe warns about: a <i>never</i> ingredient (with its optional reason) or an <i>avoid</i> one.
/// <c>IngredientName</c> is null only when the ingredient no longer exists in the catalog.
/// </summary>
public sealed record RecipeDislikeResponse(Guid IngredientId, string? IngredientName, DislikeKind Kind, string? Reason)
{
    public static RecipeDislikeResponse From(HouseholdDislike dislike, string? ingredientName) =>
        new(dislike.IngredientId, ingredientName, dislike.Kind, dislike.Reason);
}

public sealed record RecipeSectionResponse(string Name, IReadOnlyList<RecipeLineResponse> Lines);

/// <summary>
/// An ingredient line. <c>IngredientName</c> is null only when the ingredient no longer exists in the catalog;
/// <c>Quantity</c>, <c>QuantityPerServing</c> and <c>Unit</c> are all null for an ingredient added to taste.
/// </summary>
public sealed record RecipeLineResponse(
    Guid IngredientId,
    string? IngredientName,
    bool IngredientNeedsReview,
    decimal? Quantity,
    decimal? QuantityPerServing,
    Unit? Unit,
    string? Note);
