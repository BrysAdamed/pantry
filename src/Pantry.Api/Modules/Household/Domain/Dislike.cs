namespace Pantry.Api.Modules.Household.Domain;

/// <summary>
/// A household-wide ingredient preference: <see cref="DislikeKind.Never"/> (with an optional reason, such as an allergy)
/// or <see cref="DislikeKind.Avoid"/>. One per catalog ingredient.
/// </summary>
public sealed class Dislike
{
    public const int ReasonMaxLength = 100;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid IngredientId { get; set; }

    public DislikeKind Kind { get; private set; }

    public string? Reason { get; private set; }

    public static Dislike Create(Guid ingredientId, DislikeKind kind, string? reason)
    {
        var dislike = new Dislike { IngredientId = ingredientId };
        dislike.Change(kind, reason);
        return dislike;
    }

    /// <summary>Sets the kind and reason; an <see cref="DislikeKind.Avoid"/> dislike has no reason.</summary>
    public void Change(DislikeKind kind, string? reason)
    {
        Kind = kind;
        Reason = kind == DislikeKind.Never ? reason : null;
    }

    /// <summary>
    /// Takes over <paramref name="other"/> when two disliked ingredients are merged: the stricter kind stays, and a
    /// <see cref="DislikeKind.Never"/> keeps its own reason before the other one's.
    /// </summary>
    public void Absorb(Dislike other)
    {
        if (other.Kind != DislikeKind.Never)
        {
            return;
        }

        Change(DislikeKind.Never, Kind == DislikeKind.Never ? Reason ?? other.Reason : other.Reason);
    }

    public static bool IsValidReason(string? reason) => reason is null || reason.Length is > 0 and <= ReasonMaxLength;
}
