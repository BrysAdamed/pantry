using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Tests.Recipes;

public sealed class RecipeCooldownDomainTests
{
    private static readonly DateOnly LastEaten = new(2026, 11, 1);

    [Fact]
    public void Never_eaten_recipe_is_available_now()
    {
        var cooldown = RecipeCooldown.Of(recipeCooldownDays: null, householdCooldownDays: 21, lastEaten: null);

        Assert.Null(cooldown.AvailableFrom);
        Assert.False(cooldown.IsTooSoonOn(DateOnly.MinValue));
    }

    [Fact]
    public void Recipe_without_its_own_cooldown_uses_the_household_one()
    {
        var cooldown = RecipeCooldown.Of(recipeCooldownDays: null, householdCooldownDays: 21, LastEaten);

        Assert.Equal(21, cooldown.Days);
        Assert.True(cooldown.IsHouseholdDefault);
        Assert.Equal(new DateOnly(2026, 11, 22), cooldown.AvailableFrom);
    }

    [Fact]
    public void Recipe_cooldown_overrides_the_household_one()
    {
        var cooldown = RecipeCooldown.Of(recipeCooldownDays: 7, householdCooldownDays: 21, LastEaten);

        Assert.Equal(7, cooldown.Days);
        Assert.False(cooldown.IsHouseholdDefault);
        Assert.Equal(new DateOnly(2026, 11, 8), cooldown.AvailableFrom);
    }

    [Fact]
    public void Zero_days_means_no_cooldown()
    {
        var cooldown = RecipeCooldown.Of(recipeCooldownDays: 0, householdCooldownDays: 21, LastEaten);

        Assert.Equal(0, cooldown.Days);
        Assert.False(cooldown.IsTooSoonOn(LastEaten));
        Assert.False(cooldown.IsTooSoonOn(LastEaten.AddDays(1)));
    }

    [Fact]
    public void Too_soon_before_the_available_from_date_only()
    {
        var cooldown = RecipeCooldown.Of(recipeCooldownDays: 7, householdCooldownDays: 21, LastEaten);

        Assert.True(cooldown.IsTooSoonOn(LastEaten));
        Assert.True(cooldown.IsTooSoonOn(new DateOnly(2026, 11, 7)));
        Assert.False(cooldown.IsTooSoonOn(new DateOnly(2026, 11, 8)));
    }
}
