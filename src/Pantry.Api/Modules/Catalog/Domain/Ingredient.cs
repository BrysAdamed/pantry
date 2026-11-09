namespace Pantry.Api.Modules.Catalog.Domain;

/// <summary>
/// A canonical catalog item, named in the household content language and also known by its aliases. Quantities of it
/// are summed in its base unit.
/// </summary>
public sealed class Ingredient
{
    public const int NameMaxLength = 100;
    public const int MaxAliases = 20;

    private readonly List<IngredientName> _names = [];

    private Ingredient()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public string Name { get; private set; } = string.Empty;

    /// <summary>The name's comparison key, which also orders the catalog independently of the database collation.</summary>
    public string NameKey { get; private set; } = string.Empty;

    public Unit BaseUnit { get; private set; }

    public IngredientCategory Category { get; set; } = IngredientCategories.Default;

    public bool IsStaple { get; set; }

    /// <summary>Set on ingredients created from an unknown name until a person or an agent confirms them.</summary>
    public bool NeedsReview { get; set; }

    public IReadOnlyCollection<IngredientName> Names => _names;

    public IReadOnlyList<string> Aliases =>
        _names.Where(n => n.IsAlias).OrderBy(n => n.Key, StringComparer.Ordinal).Select(n => n.Value).ToList();

    public static Ingredient Create(
        string name,
        IReadOnlyCollection<string> aliases,
        Unit baseUnit,
        IngredientCategory category = IngredientCategories.Default,
        bool isStaple = false,
        bool needsReview = false)
    {
        if (!Units.IsBaseUnit(baseUnit))
        {
            throw new ArgumentException($"'{baseUnit}' is not a base unit.", nameof(baseUnit));
        }

        var ingredient = new Ingredient
        {
            BaseUnit = baseUnit,
            Category = category,
            IsStaple = isStaple,
            NeedsReview = needsReview,
        };
        ingredient.SetNames(name, aliases);
        return ingredient;
    }

    public static bool IsValidName(string name) =>
        name.Length is > 0 and <= NameMaxLength && IngredientName.KeyOf(name).Length > 0;

    /// <summary>
    /// The cleaned names that clash with each other (same key), for example an alias that only differs from the name
    /// in letter case. Empty when the names can be set.
    /// </summary>
    public static IReadOnlyList<string> RepeatedNames(string name, IReadOnlyCollection<string> aliases) =>
        aliases.Prepend(name)
            .Select(IngredientName.Clean)
            .GroupBy(IngredientName.KeyOf)
            .Where(g => g.Count() > 1)
            .Select(g => g.First())
            .ToList();

    /// <summary>
    /// Replaces the name and the aliases. Names that keep their key keep their row, so swapping the name with an alias
    /// does not trip the catalog-wide unique key.
    /// </summary>
    public void SetNames(string name, IReadOnlyCollection<string> aliases)
    {
        var cleanName = IngredientName.Clean(name);
        var cleanAliases = aliases.Select(IngredientName.Clean).ToList();
        if (!IsValidName(cleanName) || !cleanAliases.All(IsValidName) || cleanAliases.Count > MaxAliases
            || RepeatedNames(cleanName, cleanAliases).Count > 0)
        {
            throw new ArgumentException("The name and aliases must be valid and distinct.", nameof(aliases));
        }

        var wanted = cleanAliases.Select(alias => (Value: alias, IsAlias: true))
            .Prepend((Value: cleanName, IsAlias: false))
            .ToDictionary(n => IngredientName.KeyOf(n.Value));

        _names.RemoveAll(existing => !wanted.ContainsKey(existing.Key));
        foreach (var (key, (value, isAlias)) in wanted)
        {
            if (_names.FirstOrDefault(existing => existing.Key == key) is { } existing)
            {
                existing.Value = value;
                existing.IsAlias = isAlias;
            }
            else
            {
                _names.Add(new IngredientName(value, isAlias));
            }
        }

        Name = cleanName;
        NameKey = IngredientName.KeyOf(cleanName);
    }
}
