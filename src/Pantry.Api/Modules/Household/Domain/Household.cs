namespace Pantry.Api.Modules.Household.Domain;

/// <summary>The single household of a deployment (ADR 0003). Holds household-wide settings.</summary>
public sealed class Household
{
    public const int SingletonId = 1;
    public const int DefaultCooldownDays = 21;
    public const int MaxCooldownDays = 365;

    public int Id { get; init; } = SingletonId;

    /// <summary>Set by the first run from the admin's UI language; null only before it.</summary>
    public ContentLanguage? ContentLanguage { get; set; }

    /// <summary>Days before a recipe should come back, for every recipe without its own cooldown; 0 for none.</summary>
    public int CooldownDays { get; set; } = DefaultCooldownDays;

    public static bool IsValidCooldown(int days) => days is >= 0 and <= MaxCooldownDays;
}
