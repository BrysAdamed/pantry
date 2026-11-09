namespace Pantry.Api.Modules.Household.Domain;

/// <summary>The single household of a deployment (ADR 0003). Holds household-wide settings as they arrive.</summary>
public sealed class Household
{
    public const int SingletonId = 1;

    public int Id { get; init; } = SingletonId;
}
