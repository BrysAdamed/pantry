using Microsoft.Extensions.Options;
using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Modules.Recipes.Photos;

/// <summary>
/// The photo files in the folder from <see cref="RecipePhotoSettings.Path"/>: one file per photo and size, named by the
/// photo id, which changes with every upload, so a file never changes once written (ADR 0025).
/// </summary>
public sealed class RecipePhotoStore(IOptions<RecipePhotoSettings> settings, IHostEnvironment environment, ILogger<RecipePhotoStore> logger)
{
    private readonly string _root = System.IO.Path.GetFullPath(settings.Value.Path, environment.ContentRootPath);

    public async Task SaveAsync(Guid photoId, IReadOnlyDictionary<RecipePhotoSize, byte[]> sizes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        try
        {
            foreach (var (size, content) in sizes)
            {
                var path = PathOf(photoId, size);
                var temporary = path + ".tmp";
                await File.WriteAllBytesAsync(temporary, content, cancellationToken);
                File.Move(temporary, path, overwrite: true);
            }
        }
        catch
        {
            Delete(photoId);
            throw;
        }
    }

    /// <summary>The file of one size of a photo, or null when it is not there.</summary>
    public Stream? Open(Guid photoId, RecipePhotoSize size)
    {
        try
        {
            return File.OpenRead(PathOf(photoId, size));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Deletes every size of a photo. A failure is logged and left behind, because the database no longer points at it.</summary>
    public void Delete(Guid photoId)
    {
        foreach (var size in Enum.GetValues<RecipePhotoSize>())
        {
            var path = PathOf(photoId, size);
            try
            {
                File.Delete(path);
                File.Delete(path + ".tmp");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not delete the recipe photo file {Path}.", path);
            }
        }
    }

    private string PathOf(Guid photoId, RecipePhotoSize size) =>
        System.IO.Path.Combine(_root, $"{photoId:N}-{CatalogCode.Of(size)}.webp");
}
