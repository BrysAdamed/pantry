namespace Pantry.Api.Modules.Household.Domain;

/// <summary>The single household of a deployment (ADR 0003). Holds household-wide settings.</summary>
public sealed class Household
{
    public const int SingletonId = 1;

    public int Id { get; init; } = SingletonId;

    /// <summary>Set by the first run from the admin's UI language; null only before it.</summary>
    public ContentLanguage? ContentLanguage { get; set; }
}
