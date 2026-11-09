using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Tests.Household;

public sealed class MealSlotTests
{
    [Theory]
    [InlineData(ContentLanguage.En)]
    [InlineData(ContentLanguage.Pl)]
    public void Defaults_are_five_enabled_slots_in_order_with_valid_names(ContentLanguage language)
    {
        var slots = MealSlot.CreateDefaults(language);

        Assert.Equal(5, slots.Count);
        Assert.Equal([0, 1, 2, 3, 4], slots.Select(s => s.Position));
        Assert.All(slots, s => Assert.True(s.IsEnabled && MealSlot.IsValidName(s.Name)));
        Assert.Equal(5, slots.Select(s => s.Id).Distinct().Count());
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Brunch", true)]
    [InlineData("12345678901234567890123456789012345678901234567890", true)]
    [InlineData("123456789012345678901234567890123456789012345678901", false)]
    public void Name_has_1_to_50_characters(string name, bool valid) =>
        Assert.Equal(valid, MealSlot.IsValidName(name));
}
