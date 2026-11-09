namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// When a recipe should come back: its own cooldown or else the household's, counted from the day it was last eaten.
/// A soft hint: planning earlier is allowed, the UI only says <i>too soon</i>.
/// </summary>
/// <param name="Days">The cooldown in effect; 0 means none.</param>
/// <param name="IsHouseholdDefault">True when the recipe has no cooldown of its own.</param>
/// <param name="LastEaten">The date of its newest history entry; null when it was never eaten.</param>
/// <param name="AvailableFrom">The first day it is not too soon (last eaten + cooldown); null when never eaten, which means now.</param>
public sealed record RecipeCooldown(int Days, bool IsHouseholdDefault, DateOnly? LastEaten, DateOnly? AvailableFrom)
{
    public static RecipeCooldown Of(int? recipeCooldownDays, int householdCooldownDays, DateOnly? lastEaten)
    {
        var days = recipeCooldownDays ?? householdCooldownDays;
        return new RecipeCooldown(days, recipeCooldownDays is null, lastEaten, lastEaten?.AddDays(days));
    }

    /// <summary>Whether planning the recipe on <paramref name="date"/> is too soon.</summary>
    public bool IsTooSoonOn(DateOnly date) => IsTooSoon(AvailableFrom, date);

    public static bool IsTooSoon(DateOnly? availableFrom, DateOnly date) => availableFrom is { } from && date < from;
}
