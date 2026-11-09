using System.ComponentModel.DataAnnotations;

namespace Pantry.Api.Modules.Recipes.Photos;

/// <summary>Recipe photos, bound from <c>Recipes:Photos</c> (ADR 0025).</summary>
public sealed class RecipePhotoSettings
{
    public const string Section = "Recipes:Photos";
    public const int DefaultMaxBytes = 10 * 1024 * 1024;

    /// <summary>The folder photos are kept in, absolute or relative to the content root. In Docker it is a volume.</summary>
    [Required]
    public string Path { get; set; } = "data/recipe-photos";

    /// <summary>The largest upload accepted, in bytes.</summary>
    [Range(1024, 50 * 1024 * 1024)]
    public int MaxBytes { get; set; } = DefaultMaxBytes;
}
