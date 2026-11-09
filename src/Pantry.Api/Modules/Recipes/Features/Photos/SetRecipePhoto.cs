using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Features.Recipes;
using Pantry.Api.Modules.Recipes.Persistence;
using Pantry.Api.Modules.Recipes.Photos;

namespace Pantry.Api.Modules.Recipes.Features.Photos;

/// <summary>
/// Adds or replaces the photo of a recipe, archived or not. The body is the picture itself (JPEG, PNG or WebP, at most
/// <c>Recipes:Photos:MaxBytes</c>), recognised by its content rather than its content type; a larger body answers 413,
/// anything else that is not such a picture 415. <c>DELETE</c> removes the photo. Both answer the recipe.
/// </summary>
public static class SetRecipePhoto
{
    public static IEndpointRouteBuilder MapSetRecipePhoto(this IEndpointRouteBuilder recipes)
    {
        recipes.MapPut("/{id:guid}/photo", SetAsync);
        recipes.MapDelete("/{id:guid}/photo", RemoveAsync);
        return recipes;
    }

    private static async Task<Results<Ok<RecipeResponse>, NotFound, StatusCodeHttpResult>> SetAsync(
        Guid id, HttpRequest request, RecipePhotos photos, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, CancellationToken cancellationToken)
    {
        if (request.ContentLength > photos.MaxBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        return await photos.SetAsync(id, request.Body, cancellationToken) switch
        {
            RecipePhotoResult.Saved => TypedResults.Ok(await ResponseAsync(id, db, catalog, household, cancellationToken)),
            RecipePhotoResult.TooLarge => TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge),
            RecipePhotoResult.NotAPhoto => TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType),
            _ => TypedResults.NotFound(),
        };
    }

    private static async Task<Results<Ok<RecipeResponse>, NotFound>> RemoveAsync(
        Guid id, RecipePhotos photos, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, CancellationToken cancellationToken) =>
        await photos.RemoveAsync(id, cancellationToken)
            ? TypedResults.Ok(await ResponseAsync(id, db, catalog, household, cancellationToken))
            : TypedResults.NotFound();

    private static async Task<RecipeResponse> ResponseAsync(
        Guid id, RecipesDbContext db, ICatalogModule catalog, IHouseholdModule household, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.AsNoTracking().AsSplitQuery().SingleAsync(r => r.Id == id, cancellationToken);
        return await RecipeResponse.FromAsync(recipe, db, catalog, household, cancellationToken);
    }
}
