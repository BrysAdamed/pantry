using Pantry.Api.Infrastructure.Text;

namespace Pantry.Api.Modules.Recipes.Domain;

/// <summary>
/// A dish with ingredient lines (entered for <see cref="Servings"/>, also kept per one serving), steps, tags and optional
/// nutrition per serving. Recipes are archived, never deleted, so plan entries and history keep pointing at them.
/// </summary>
public sealed class Recipe
{
    public const char SearchKeySeparator = '\n';

    private readonly List<RecipeTag> _tags = [];
    private readonly List<RecipeStep> _steps = [];
    private readonly List<RecipeIngredientLine> _lines = [];

    private Recipe()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public string Name { get; private set; } = string.Empty;

    /// <summary>How many servings the quantities were entered for (<i>recipe servings</i>).</summary>
    public int Servings { get; private set; }

    public int? PrepTimeMinutes { get; private set; }

    public string? SourceUrl { get; private set; }

    public Nutrition Nutrition { get; private set; } = Nutrition.None;

    public bool MakeAgain { get; private set; } = true;

    /// <summary>Days before the recipe should be planned again, overriding the household default; null to use the default.</summary>
    public int? CooldownDays { get; private set; }

    /// <summary>
    /// The name and the tags in comparison form (<see cref="TextKey"/>), one per line, so the library can search them
    /// ignoring letter case and diacritics. A key never holds a line break, so a search cannot match across two.
    /// </summary>
    public string SearchKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>The photo, a new id with every upload so its files never change; null for none. Archiving keeps it.</summary>
    public Guid? PhotoId { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyCollection<RecipeTag> Tags => _tags;

    public IReadOnlyCollection<RecipeStep> Steps => _steps;

    public IReadOnlyCollection<RecipeIngredientLine> Lines => _lines;

    public IReadOnlyList<string> TagValues() => _tags.Select(t => t.Value).Order(StringComparer.Ordinal).ToList();

    public IReadOnlyList<string> StepTexts() => _steps.OrderBy(s => s.Position).Select(s => s.Text).ToList();

    public IReadOnlyList<RecipeIngredientLine> OrderedLines() => _lines.OrderBy(l => l.Position).ToList();

    public static Recipe Create(RecipeContent content, DateTimeOffset now)
    {
        var recipe = new Recipe { CreatedAt = now };
        recipe.Update(content, now);
        return recipe;
    }

    /// <summary>Replaces everything a person or an agent edits. Rows keep their place, so a save rewrites only what changed.</summary>
    public void Update(RecipeContent content, DateTimeOffset now)
    {
        if (content.Problems() is { Count: > 0 } problems)
        {
            throw new ArgumentException($"The recipe is not valid: {string.Join(", ", problems)}.", nameof(content));
        }

        Name = content.Name;
        Servings = content.Servings;
        PrepTimeMinutes = content.PrepTimeMinutes;
        SourceUrl = content.SourceUrl;
        Nutrition = content.Nutrition.Rounded();
        MakeAgain = content.MakeAgain;
        CooldownDays = content.CooldownDays;
        SetTags(content.Tags);
        SetSteps(content.Steps);
        SetLines(content.Lines);
        SearchKey = string.Join(SearchKeySeparator, TagValues().Prepend(Name).Select(TextKey.Of));
        UpdatedAt = now;
    }

    /// <summary>Sets <i>make again</i> on its own: from the detail, or from a rating that answered it (the latest answer wins).</summary>
    public void SetMakeAgain(bool makeAgain, DateTimeOffset now)
    {
        if (MakeAgain == makeAgain)
        {
            return;
        }

        MakeAgain = makeAgain;
        UpdatedAt = now;
    }

    /// <summary>Puts <paramref name="photoId"/> in place of the current photo and returns the replaced one, whose files can go.</summary>
    public Guid? SetPhoto(Guid? photoId, DateTimeOffset now)
    {
        var replaced = PhotoId;
        if (replaced == photoId)
        {
            return null;
        }

        PhotoId = photoId;
        UpdatedAt = now;
        return replaced;
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return;
        }

        ArchivedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Points the lines of <paramref name="sourceId"/> at <paramref name="targetId"/> after an ingredient merge. Quantities,
    /// units, notes and positions stay, so a recipe that had lines for both keeps two lines.
    /// </summary>
    public void ReplaceIngredient(Guid sourceId, Guid targetId)
    {
        foreach (var line in _lines.Where(l => l.IngredientId == sourceId))
        {
            line.ReplaceIngredient(targetId);
        }
    }

    private void SetTags(IReadOnlyList<string> tags)
    {
        _tags.RemoveAll(existing => !tags.Contains(existing.Value));
        foreach (var tag in tags.Where(tag => _tags.All(existing => existing.Value != tag)))
        {
            _tags.Add(new RecipeTag(tag));
        }
    }

    private void SetSteps(IReadOnlyList<string> steps)
    {
        _steps.RemoveAll(existing => existing.Position >= steps.Count);
        for (var position = 0; position < steps.Count; position++)
        {
            var step = _steps.FirstOrDefault(s => s.Position == position);
            if (step is null)
            {
                _steps.Add(new RecipeStep(position, steps[position]));
            }
            else
            {
                step.Text = steps[position];
            }
        }
    }

    private void SetLines(IReadOnlyList<RecipeLine> lines)
    {
        _lines.RemoveAll(existing => existing.Position >= lines.Count);
        for (var position = 0; position < lines.Count; position++)
        {
            var line = _lines.FirstOrDefault(l => l.Position == position);
            if (line is null)
            {
                line = new RecipeIngredientLine(position);
                _lines.Add(line);
            }

            line.Set(lines[position], Servings);
        }
    }
}
