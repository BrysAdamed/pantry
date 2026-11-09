namespace Pantry.Api.Modules.Household.Domain;

/// <summary>A meal of the day that the planner shows as a row. Never deleted: disabling keeps plan history intact.</summary>
public sealed class MealSlot
{
    public const int NameMaxLength = 50;

    private static readonly Dictionary<ContentLanguage, string[]> DefaultNames = new()
    {
        [ContentLanguage.Pl] = ["Śniadanie", "II śniadanie", "Obiad", "Podwieczorek", "Kolacja"],
        [ContentLanguage.En] = ["Breakfast", "Second breakfast", "Lunch", "Snack", "Dinner"],
    };

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public int Position { get; set; }

    public bool IsEnabled { get; set; } = true;

    public static bool IsValidName(string name) => name.Length is > 0 and <= NameMaxLength;

    /// <summary>The slots a new household starts with, named in <paramref name="language"/>. Afterwards the names are household data.</summary>
    public static IReadOnlyList<MealSlot> CreateDefaults(ContentLanguage language) =>
        DefaultNames[language].Select((name, position) => new MealSlot { Name = name, Position = position }).ToList();
}
