namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// What merging the source into the target changes in the catalog: the source's name and aliases become aliases of
/// the target. The target keeps its name, base unit, category and staple flag.
/// </summary>
public sealed record IngredientMerge(Guid TargetId, Guid SourceId, IReadOnlyList<string> AliasesToAdd, bool SameBaseUnit)
{
    public static IngredientMerge Plan(Ingredient target, Ingredient source)
    {
        if (target.Id == source.Id)
        {
            throw new ArgumentException("An ingredient cannot be merged into itself.", nameof(source));
        }

        var targetKeys = target.Names.Select(n => n.Key).ToHashSet();
        var aliases = source.Aliases.Prepend(source.Name)
            .Where(name => !targetKeys.Contains(IngredientName.KeyOf(name)))
            .ToList();
        return new IngredientMerge(target.Id, source.Id, aliases, target.BaseUnit == source.BaseUnit);
    }

    /// <summary>How many aliases the target has after the merge.</summary>
    public int AliasCountAfter(Ingredient target) => target.Aliases.Count + AliasesToAdd.Count;

    public bool ExceedsAliasLimit(Ingredient target) => AliasCountAfter(target) > Ingredient.MaxAliases;
}
