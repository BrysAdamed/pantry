namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// A record that the household ate a recipe: one per eaten plan entry, written by the Planner through
/// <see cref="IRecipesModule"/>. Leftovers have none.
/// </summary>
public sealed class HistoryEntry
{
    private HistoryEntry()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PlanEntryId { get; private init; }

    public Guid RecipeId { get; private set; }

    public DateOnly Date { get; private set; }

    public Guid MealSlotId { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public static HistoryEntry Create(EatenPlanEntry entry, DateTimeOffset now)
    {
        var history = new HistoryEntry { PlanEntryId = entry.PlanEntryId };
        history.Update(entry, now);
        return history;
    }

    /// <summary>Takes the recipe, date and meal slot of the plan entry again, which may have changed since it was recorded.</summary>
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

        RecipeId = entry.RecipeId;
        Date = entry.Date;
        MealSlotId = entry.MealSlotId;
        RecordedAt = now;
    }
}
