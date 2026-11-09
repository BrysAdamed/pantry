using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Household.Domain;

namespace Pantry.Api.Tests.Household;

public sealed class HouseholdSettingsDomainTests
{
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData(" DE ", "de")]
    [InlineData("En", "en")]
    public void Content_language_parses_supported_codes_case_insensitively(string input, string code)
    {
        Assert.True(ContentLanguage.TryParse(input, out var language));
        Assert.Equal(code, language.Code);
        Assert.Same(ContentLanguage.Parse(code), language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    [InlineData("pl-PL")]
    public void Content_language_rejects_codes_outside_the_supported_list(string? input)
    {
        Assert.False(ContentLanguage.TryParse(input, out _));
        Assert.Throws<FormatException>(() => ContentLanguage.Parse(input ?? string.Empty));
    }

    [Fact]
    public void Supported_content_languages_are_unique_lowercase_codes()
    {
        var codes = ContentLanguage.Supported.Select(l => l.Code).ToList();

        Assert.Equal(codes.Distinct().Count(), codes.Count);
        Assert.All(codes, code => Assert.Equal(code.ToLowerInvariant(), code));
        Assert.All(codes, code => Assert.InRange(code.Length, 2, ContentLanguage.CodeMaxLength));
        Assert.Contains(ContentLanguage.Default, ContentLanguage.Supported);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Planning_rule_text_has_1_to_200_characters(int length, bool valid) =>
        Assert.Equal(valid, PlanningRule.IsValidText(new string('a', length)));
}
