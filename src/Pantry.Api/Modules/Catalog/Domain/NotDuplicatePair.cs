namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// Two ingredients a person or an agent marked as <i>not duplicates</i>; the pair is no longer suggested as a possible
/// duplicate. The ids are stored in the database's uuid order, so a pair has one row whichever way it was marked.
/// Deleting either ingredient deletes the pair.
/// </summary>
public sealed class NotDuplicatePair
{
    private NotDuplicatePair()
    {
    }

    public Guid FirstIngredientId { get; private init; }

    public Guid SecondIngredientId { get; private init; }

    public static NotDuplicatePair Of(Guid oneIngredientId, Guid otherIngredientId)
    {
        if (oneIngredientId == otherIngredientId)
        {
            throw new ArgumentException("An ingredient cannot be paired with itself.", nameof(otherIngredientId));
        }

        var (first, second) = PostgresOrder(oneIngredientId, otherIngredientId) < 0
            ? (oneIngredientId, otherIngredientId)
            : (otherIngredientId, oneIngredientId);
        return new NotDuplicatePair { FirstIngredientId = first, SecondIngredientId = second };
    }

    /// <summary>Postgres compares uuids byte by byte, which is the order of their lower-case text form.</summary>
    private static int PostgresOrder(Guid a, Guid b) => string.CompareOrdinal(a.ToString("D"), b.ToString("D"));
}
