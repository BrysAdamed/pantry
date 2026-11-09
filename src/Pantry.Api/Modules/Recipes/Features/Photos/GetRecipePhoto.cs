using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Infrastructure.Http;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Persistence;
using Pantry.Api.Modules.Recipes.Photos;

namespace Pantry.Api.Modules.Recipes.Features.Photos;

/// <summary>
/// One size of the current photo of a recipe, archived or not. A replaced or removed photo is gone (404). The URL never
/// serves other bytes, so the browser may keep the response privately for a year without asking again.
/// </summary>
public static class GetRecipePhoto
{
    public static RouteHandlerBuilder MapGetRecipePhoto(this IEndpointRouteBuilder recipes) =>
        recipes.MapGet("/{id:guid}/photos/{photoId:guid}/{size}", HandleAsync).CacheImmutably();

    private static async Task<Results<FileStreamHttpResult, NotFound>> HandleAsync(
        Guid id, Guid photoId, string size, RecipesDbContext db, RecipePhotoStore store, CancellationToken cancellationToken)
    {
        if (!CatalogCode.TryParse<RecipePhotoSize>(size, out var parsed)
            || !await db.Recipes.AnyAsync(r => r.Id == id && r.PhotoId == photoId, cancellationToken)
            || store.Open(photoId, parsed.Value) is not { } file)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Stream(file, RecipePhotoProcessor.ContentType);
    }
}
