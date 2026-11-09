using System.Text.Json;
using Pantry.Api.Modules.Catalog;

namespace Pantry.Api.Tests.Catalog;

public sealed class CatalogLabelsTests
{
    public static TheoryData<string> Languages => new() { "en", "pl" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_category_and_unit_has_a_label(string language)
    {
        // Arrange
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindI18nDirectory(), $"{language}.json")));
        var catalog = file.RootElement.GetProperty("catalog");

        // Act
        var categories = Labels(catalog.GetProperty("category"));
        var units = Labels(catalog.GetProperty("unit"));

        // Assert
        Assert.Equal(IngredientCategories.Ordered.Select(CatalogCode.Of).Order(), categories.Keys.Order());
        Assert.Equal(Units.Ordered.Select(CatalogCode.Of).Order(), units.Keys.Order());
        Assert.All(categories.Values.Concat(units.Values), label => Assert.False(string.IsNullOrWhiteSpace(label)));
    }

    private static Dictionary<string, string?> Labels(JsonElement section) =>
        section.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());

    private static string FindI18nDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var i18n = Path.Combine(directory.FullName, "src", "Pantry.Web", "public", "i18n");
            if (Directory.Exists(i18n))
            {
                return i18n;
            }
        }

        throw new DirectoryNotFoundException("src/Pantry.Web/public/i18n not found above the test output directory.");
    }
}
