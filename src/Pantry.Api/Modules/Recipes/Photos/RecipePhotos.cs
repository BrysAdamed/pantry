using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pantry.Api.Modules.Recipes.Domain;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes.Photos;

/// <summary>Adds, replaces and removes the photo of a recipe for the REST endpoints and <see cref="IRecipesModule"/>.</summary>
public sealed class RecipePhotos(RecipesDbContext db, RecipePhotoStore store, IOptions<RecipePhotoSettings> settings, TimeProvider time)
{
    public int MaxBytes => settings.Value.MaxBytes;

    /// <summary>
    /// Makes <paramref name="upload"/> the recipe's photo. The new files are written before the recipe points at them and
    /// the replaced photo's files go only after it no longer does, so a failure never leaves a recipe without its files.
    /// </summary>
    public async Task<RecipePhotoResult> SetAsync(Guid recipeId, Stream upload, CancellationToken cancellationToken)
    {
        if (!await db.Recipes.AnyAsync(r => r.Id == recipeId, cancellationToken))
        {
            return RecipePhotoResult.RecipeNotFound;
        }

        var content = await ReadAsync(upload, cancellationToken);
        if (content is null)
        {
            return RecipePhotoResult.TooLarge;
        }

        var sizes = RecipePhotoProcessor.Process(content);
        if (sizes is null)
        {
            return RecipePhotoResult.NotAPhoto;
        }

        var photoId = Guid.CreateVersion7();
        await store.SaveAsync(photoId, sizes, cancellationToken);
        bool changed;
        try
        {
            changed = await ChangeAsync(recipeId, photoId, cancellationToken);
        }
        catch
        {
            store.Delete(photoId);
            throw;
        }

        if (!changed)
        {
            store.Delete(photoId);
            return RecipePhotoResult.RecipeNotFound;
        }

        return RecipePhotoResult.Saved;
    }

    /// <summary>Removes the recipe's photo, if it has one; false when the recipe does not exist.</summary>
    public Task<bool> RemoveAsync(Guid recipeId, CancellationToken cancellationToken) => ChangeAsync(recipeId, null, cancellationToken);

    private async Task<bool> ChangeAsync(Guid recipeId, Guid? photoId, CancellationToken cancellationToken)
    {
        Guid? replaced;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var recipe = await LockAsync(recipeId, cancellationToken);
            if (recipe is null)
            {
                return false;
            }

            replaced = recipe.SetPhoto(photoId, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (replaced is { } old)
        {
            store.Delete(old);
        }

        return true;
    }

    private async Task<Recipe?> LockAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM recipes.recipes WHERE id = {recipeId} FOR UPDATE", cancellationToken);
        return await db.Recipes.AsSplitQuery().SingleOrDefaultAsync(r => r.Id == recipeId, cancellationToken);
    }

    private async Task<byte[]?> ReadAsync(Stream upload, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await upload.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
