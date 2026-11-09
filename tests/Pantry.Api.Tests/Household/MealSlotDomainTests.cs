using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Tests.Household;

public sealed class MealSlotDomainTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    [InlineData("de")]
    public void Defaults_are_five_enabled_slots_in_order_with_valid_names(string code)
    {
        var slots = MealSlot.CreateDefaults(ContentLanguage.Parse(code));

        Assert.Equal(5, slots.Count);
        Assert.Equal([0, 1, 2, 3, 4], slots.Select(s => s.Position));
        Assert.All(slots, s => Assert.True(s.IsEnabled && MealSlot.IsValidName(s.Name)));
        Assert.Equal(5, slots.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Defaults_fall_back_to_english_names_for_a_language_without_built_in_names() =>
        Assert.Equal(
            MealSlot.CreateDefaults(ContentLanguage.En).Select(s => s.Name),
            MealSlot.CreateDefaults(ContentLanguage.Cs).Select(s => s.Name));

    [Theory]
    [InlineData("", false)]
    [InlineData("Brunch", true)]
    [InlineData("12345678901234567890123456789012345678901234567890", true)]
    [InlineData("123456789012345678901234567890123456789012345678901", false)]
    public void Name_has_1_to_50_characters(string name, bool valid) =>
        Assert.Equal(valid, MealSlot.IsValidName(name));
}
