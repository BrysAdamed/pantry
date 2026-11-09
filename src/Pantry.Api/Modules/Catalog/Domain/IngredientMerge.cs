namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// What merging the source into the target changes in the catalog: the source's name and aliases become aliases of
/// the target, and with equal base units the source's unit conversions the target lacks are copied (ADR 0020). The other
/// source conversions are skipped. The target keeps its name, base unit, category, staple flag and own conversions.
/// </summary>
public sealed record IngredientMerge(
    Guid TargetId,
    Guid SourceId,
    IReadOnlyList<string> AliasesToAdd,
    bool SameBaseUnit,
    IReadOnlyList<(Unit Unit, decimal Amount)> ConversionsToAdd,
    IReadOnlyList<(Unit Unit, decimal Amount)> ConversionsSkipped)
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
        var sameBaseUnit = target.BaseUnit == source.BaseUnit;
        var targetUnits = target.Conversions.Select(c => c.Unit).ToHashSet();
        var conversions = source.Conversions.OrderBy(c => c.Unit).Select(c => (c.Unit, c.Amount)).ToList();
        var toAdd = sameBaseUnit ? conversions.Where(c => !targetUnits.Contains(c.Unit)).ToList() : [];
        return new IngredientMerge(target.Id, source.Id, aliases, sameBaseUnit, toAdd, conversions.Except(toAdd).ToList());
    }

    /// <summary>How many aliases the target has after the merge.</summary>
    public int AliasCountAfter(Ingredient target) => target.Aliases.Count + AliasesToAdd.Count;

    public bool ExceedsAliasLimit(Ingredient target) => AliasCountAfter(target) > Ingredient.MaxAliases;
}
