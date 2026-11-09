namespace Pantry.Api.Modules.Recipes.Photos;

/// <summary>The sizes a recipe photo is kept in. The code (<c>card</c>, <c>detail</c>) is the last segment of its URL.</summary>
public enum RecipePhotoSize
{
    /// <summary>A square crop for library cards and other small tiles.</summary>
    Card,

    /// <summary>The whole picture for the recipe detail, at most <see cref="RecipePhotoProcessor.DetailMaxSide"/> on its longer side.</summary>
    Detail,
}
