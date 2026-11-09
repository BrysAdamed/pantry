namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// A record that the household ate a recipe: one per eaten plan entry, written by the Planner through
/// <see cref="IRecipesModule"/>. Leftovers have none. The household can rate it with 1 to 5 stars.
/// </summary>
public sealed class HistoryEntry
{
    public const int MinStars = 1;
    public const int MaxStars = 5;

    private HistoryEntry()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PlanEntryId { get; private init; }

    public Guid RecipeId { get; private set; }

    public DateOnly Date { get; private set; }

    public Guid MealSlotId { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>Stars from <see cref="MinStars"/> to <see cref="MaxStars"/>; null while not rated.</summary>
    public int? Rating { get; private set; }

    public DateTimeOffset? RatedAt { get; private set; }

    public static bool IsValidRating(int stars) => stars is >= MinStars and <= MaxStars;

    public static HistoryEntry Create(EatenPlanEntry entry, DateTimeOffset now)
    {
        var history = new HistoryEntry { PlanEntryId = entry.PlanEntryId };
        history.Update(entry, now);
        return history;
    }

    /// <summary>
    /// Takes the recipe, date and meal slot of the plan entry again, which may have changed since it was recorded.
    /// A rating belongs to the dish, so another recipe drops it.
    /// </summary>
    public void Update(EatenPlanEntry entry, DateTimeOffset now)
    {
        if (entry.PlanEntryId != PlanEntryId)
        {
            throw new ArgumentException("The history entry belongs to another plan entry.", nameof(entry));
        }

        if (RecipeId == entry.RecipeId && Date == entry.Date && MealSlotId == entry.MealSlotId)
        {
            return;
        }

        if (RecipeId != entry.RecipeId)
        {
            RemoveRating();
        }

        RecipeId = entry.RecipeId;
        Date = entry.Date;
        MealSlotId = entry.MealSlotId;
        RecordedAt = now;
    }

    public void Rate(int stars, DateTimeOffset now)
    {
        if (!IsValidRating(stars))
        {
            throw new ArgumentOutOfRangeException(nameof(stars), stars, $"A rating is {MinStars} to {MaxStars} stars.");
        }

        Rating = stars;
        RatedAt = now;
    }

    public void RemoveRating()
    {
        Rating = null;
        RatedAt = null;
    }
}
