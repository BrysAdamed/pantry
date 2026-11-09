namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// Everything a person or an agent edits on a recipe, already cleaned: tags normalized, texts trimmed. Lines come in
/// display order: those outside any section first, then each section's lines together.
/// </summary>
public sealed record RecipeContent(
    string Name,
    int Servings,
    int? PrepTimeMinutes,
    string? SourceUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Steps,
    Nutrition Nutrition,
    bool MakeAgain,
    int? CooldownDays,
    IReadOnlyList<RecipeLine> Lines)
{
    public const int NameMaxLength = 200;
    public const int MaxServings = 100;
    public const int MaxPrepTimeMinutes = 10_080;
    public const int SourceUrlMaxLength = 2000;
    public const int MaxCooldownDays = 365;
    public const int MaxTags = 20;
    public const int MaxSteps = 100;
    public const int MaxLines = 100;
    public const int SectionNameMaxLength = 100;

    public static bool IsValidName(string name) => name.Length is > 0 and <= NameMaxLength;

    public static bool IsValidServings(int servings) => servings is > 0 and <= MaxServings;

    public static bool IsValidPrepTime(int? minutes) => minutes is null or (> 0 and <= MaxPrepTimeMinutes);

    public static bool IsValidCooldown(int? days) => days is null or (>= 0 and <= MaxCooldownDays);

    public static bool IsValidSectionName(string name) => name.Length is > 0 and <= SectionNameMaxLength;

    /// <summary>An absolute <c>http</c> or <c>https</c> address of at most <see cref="SourceUrlMaxLength"/> characters.</summary>
    public static bool IsValidSourceUrl(string url) =>
        url.Length <= SourceUrlMaxLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Section names compare ignoring letter case.</summary>
    public static string SectionKey(string name) => name.ToLowerInvariant();

    /// <summary>What breaks the rules, in plain words; empty when the content can be saved.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (!IsValidName(Name))
        {
            problems.Add("name");
        }

        if (!IsValidServings(Servings))
        {
            problems.Add("servings");
        }

        if (!IsValidPrepTime(PrepTimeMinutes))
        {
            problems.Add("prep time");
        }

        if (SourceUrl is not null && !IsValidSourceUrl(SourceUrl))
        {
            problems.Add("source URL");
        }

        if (!IsValidCooldown(CooldownDays))
        {
            problems.Add("cooldown");
        }

        if (Tags.Count > MaxTags || !Tags.All(t => RecipeTag.IsValid(t) && RecipeTag.Normalize(t) == t) || Tags.Distinct().Count() != Tags.Count)
        {
            problems.Add("tags");
        }

        if (Steps.Count > MaxSteps || !Steps.All(RecipeStep.IsValidText))
        {
            problems.Add("steps");
        }

        if (!new[] { Nutrition.Kcal, Nutrition.Protein, Nutrition.Fat, Nutrition.Carbs }.All(Nutrition.IsValidValue))
        {
            problems.Add("nutrition");
        }

        if (Lines.Count > MaxLines || !Lines.All(IsValidLine))
        {
            problems.Add("ingredient lines");
        }

        if (!AreSectionsGrouped(Lines.Select(l => l.Section).ToList()))
        {
            problems.Add("sections");
        }

        return problems;
    }

    public static bool IsValidLine(RecipeLine line) =>
        line.IngredientId != Guid.Empty
        && (line.Quantity is null) == (line.Unit is null)
        && (line.Quantity is not { } quantity || RecipeQuantities.IsValidQuantity(quantity))
        && (line.Note is null || (line.Note.Length > 0 && RecipeIngredientLine.IsValidNote(line.Note)))
        && (line.Section is null || IsValidSectionName(line.Section));

    /// <summary>
    /// True when lines outside any section come first and each section's lines follow one another, so the order of
    /// the lines also gives the order of the sections.
    /// </summary>
    public static bool AreSectionsGrouped(IReadOnlyList<string?> sections)
    {
        var closed = new HashSet<string>();
        string? current = null;
        var inSections = false;
        foreach (var section in sections)
        {
            if (section is null)
            {
                if (inSections)
                {
                    return false;
                }

                continue;
            }

            var key = SectionKey(section);
            inSections = true;
            if (key == current)
            {
                continue;
            }

            if (current is not null)
            {
                closed.Add(current);
            }

            if (closed.Contains(key))
            {
                return false;
            }

            current = key;
        }

        return true;
    }
}
