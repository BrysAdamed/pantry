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
    private readonly List<UnitConversion> _conversions = [];

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

    /// <summary>The ingredient's own unit conversions, each into the base unit.</summary>
    public IReadOnlyCollection<UnitConversion> Conversions => _conversions;

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

    /// <summary>
    /// An ingredient for a name nobody has used before (from the recipe editor or an agent): base unit guessed from the
    /// unit it was used with, category <c>other</c>, needs review.
    /// </summary>
    public static Ingredient CreateFromUnknownName(string name, Unit? unitUsed) =>
        Create(name, [], Units.GuessBaseUnit(unitUsed), needsReview: true);

    /// <summary>A person or an agent checked the ingredient; it no longer needs review.</summary>
    public void Confirm() => NeedsReview = false;

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

    /// <summary>Changes the base unit. The conversions are written in the old base unit, so they are removed.</summary>
    public void SetBaseUnit(Unit baseUnit)
    {
        if (!Units.IsBaseUnit(baseUnit))
        {
            throw new ArgumentException($"'{baseUnit}' is not a base unit.", nameof(baseUnit));
        }

        if (baseUnit == BaseUnit)
        {
            return;
        }

        BaseUnit = baseUnit;
        _conversions.Clear();
    }

    /// <summary>
    /// What is wrong with <paramref name="conversions"/> for <paramref name="baseUnit"/>: a repeated unit, a unit that
    /// is the base unit or converts into it anyway, or an amount that is not positive, too big or too precise.
    /// Empty when they can be set.
    /// </summary>
    public static IReadOnlyList<string> ConversionProblems(Unit baseUnit, IReadOnlyCollection<(Unit Unit, decimal Amount)> conversions)
    {
        var problems = new List<string>();
        foreach (var repeated in conversions.GroupBy(c => c.Unit).Where(g => g.Count() > 1))
        {
            problems.Add($"A unit can have one conversion: {CatalogCode.Of(repeated.Key)}.");
        }

        foreach (var (unit, amount) in conversions)
        {
            if (UnitConversion.IsRedundant(unit, baseUnit))
            {
                problems.Add($"{CatalogCode.Of(unit)} already converts into {CatalogCode.Of(baseUnit)}.");
            }

            if (!UnitConversion.IsValidAmount(amount))
            {
                problems.Add(
                    $"The amount for {CatalogCode.Of(unit)} must be greater than 0, at most {UnitConversion.MaxAmount} " +
                    $"and have at most {UnitConversion.AmountScale} decimal places.");
            }
        }

        return problems;
    }

    /// <summary>Replaces the conversions; each amount is in the base unit. A unit that keeps its conversion keeps its row.</summary>
    public void SetConversions(IReadOnlyCollection<(Unit Unit, decimal Amount)> conversions)
    {
        if (ConversionProblems(BaseUnit, conversions).Count > 0)
        {
            throw new ArgumentException("The conversions must be valid, distinct and not redundant.", nameof(conversions));
        }

        _conversions.RemoveAll(existing => conversions.All(c => c.Unit != existing.Unit));
        foreach (var (unit, amount) in conversions)
        {
            if (_conversions.FirstOrDefault(existing => existing.Unit == unit) is { } existing)
            {
                existing.Amount = amount;
            }
            else
            {
                _conversions.Add(new UnitConversion(unit, amount));
            }
        }
    }

    public IReadOnlyDictionary<Unit, decimal> ConversionAmounts() => _conversions.ToDictionary(c => c.Unit, c => c.Amount);
}
