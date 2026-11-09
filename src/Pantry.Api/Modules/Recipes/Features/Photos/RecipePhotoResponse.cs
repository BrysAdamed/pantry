using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Photos;

namespace Pantry.Api.Modules.Recipes.Features.Photos;

/// <summary>
/// Where the sizes of a recipe's photo are served. The URLs hold the photo id, which changes with every upload, so they
/// can be cached for good.
/// </summary>
public sealed record RecipePhotoResponse(string Card, string Detail)
{
    public static RecipePhotoResponse? Of(Recipe recipe) => recipe.PhotoId is { } photoId
        ? new RecipePhotoResponse(UrlOf(recipe.Id, photoId, RecipePhotoSize.Card), UrlOf(recipe.Id, photoId, RecipePhotoSize.Detail))
        : null;

    public static string UrlOf(Guid recipeId, Guid photoId, RecipePhotoSize size) =>
        $"/api/recipes/{recipeId}/photos/{photoId}/{CatalogCode.Of(size)}";
}
