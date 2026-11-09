using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>A name or alias that another ingredient already has.</summary>
public sealed record NameConflict(string Name, Guid IngredientId, string IngredientName);

internal static class IngredientValidation
{
    /// <summary>Checks and cleans the name and aliases, adding what is wrong to <paramref name="errors"/>.</summary>
    public static void ValidateNames(
        string? name,
        IReadOnlyList<string?>? aliases,
        Dictionary<string, string[]> errors,
        out string cleanName,
        out IReadOnlyList<string> cleanAliases)
    {
        cleanName = IngredientName.Clean(name ?? string.Empty);
        cleanAliases = (aliases ?? []).Select(a => IngredientName.Clean(a ?? string.Empty)).ToList();
        if (!Ingredient.IsValidName(cleanName))
        {
            errors["Name"] = [$"Name must have 1 to {Ingredient.NameMaxLength} characters."];
        }

        if (cleanAliases.Count > Ingredient.MaxAliases)
        {
            errors["Aliases"] = [$"An ingredient can have at most {Ingredient.MaxAliases} aliases."];
        }
        else if (!cleanAliases.All(Ingredient.IsValidName))
        {
            errors["Aliases"] = [$"Each alias must have 1 to {Ingredient.NameMaxLength} characters."];
        }
        else if (!errors.ContainsKey("Name") && Ingredient.RepeatedNames(cleanName, cleanAliases) is { Count: > 0 } repeated)
        {
            errors["Aliases"] = [$"Aliases must differ from the name and from each other: {string.Join(", ", repeated)}."];
        }
    }

    /// <summary>
    /// Saves the ingredient, or returns the 409 for the names another ingredient already has. The unique key on names
    /// catches the race the check before the save cannot.
    /// </summary>
    public static async Task<ProblemHttpResult?> SaveAsync(CatalogDbContext db, Ingredient ingredient, CancellationToken cancellationToken)
    {
        if (await FindConflictsAsync(db, ingredient, cancellationToken) is { Count: > 0 } conflicts)
        {
            return Conflict(conflicts);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (CatalogDbContext.IsDuplicateName(exception))
        {
            return Conflict(await FindConflictsAsync(db, ingredient, cancellationToken));
        }
    }

    /// <summary>The names of <paramref name="ingredient"/> that other ingredients already use.</summary>
    private static async Task<IReadOnlyList<NameConflict>> FindConflictsAsync(
        CatalogDbContext db, Ingredient ingredient, CancellationToken cancellationToken)
    {
        var keys = ingredient.Names.Select(n => n.Key).ToList();
        var others = await db.Ingredients.AsNoTracking()
            .Where(i => i.Id != ingredient.Id && i.Names.Any(n => keys.Contains(n.Key)))
            .ToListAsync(cancellationToken);
        return ingredient.Names
            .SelectMany(name => others
                .Where(other => other.Names.Any(n => n.Key == name.Key))
                .Select(other => new NameConflict(name.Value, other.Id, other.Name)))
            .ToList();
    }

    private static ProblemHttpResult Conflict(IReadOnlyList<NameConflict> conflicts) => TypedResults.Problem(
        title: "Another ingredient already has this name.",
        detail: "Names and aliases are unique in the catalog, regardless of letter case and diacritics.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["conflicts"] = conflicts });
}
