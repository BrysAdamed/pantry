namespace Pantry.Api.Modules.Household.Domain;

/// <summary>A free-text rule agents follow when planning. A disabled rule is kept but agents do not see it.</summary>
public sealed class PlanningRule
{
    public const int TextMaxLength = 200;

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Text { get; set; }

    public int Position { get; set; }

    public bool IsEnabled { get; set; } = true;

    public static bool IsValidText(string text) => text.Length is > 0 and <= TextMaxLength;
}
