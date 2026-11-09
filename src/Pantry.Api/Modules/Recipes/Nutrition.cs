namespace Pantry.Api.Modules.Recipes;

/// <summary>Nutrition of one serving: energy in kcal, protein, fat and carbohydrates in grams. Each value is optional.</summary>
public sealed record Nutrition(decimal? Kcal, decimal? Protein, decimal? Fat, decimal? Carbs)
{
    public const decimal MaxValue = 100_000m;
    public const int Scale = 1;

    public static Nutrition None { get; } = new(null, null, null, null);

    public bool IsEmpty => Kcal is null && Protein is null && Fat is null && Carbs is null;

    public static bool IsValidValue(decimal? value) => value is null or (>= 0 and <= MaxValue);

    /// <summary>The same values rounded to <see cref="Scale"/> decimal places, as agents often compute them from a whole recipe.</summary>
    public Nutrition Rounded() => new(Round(Kcal), Round(Protein), Round(Fat), Round(Carbs));

    private static decimal? Round(decimal? value) =>
        value is { } v ? decimal.Round(v, Scale, MidpointRounding.AwayFromZero) : null;
}
