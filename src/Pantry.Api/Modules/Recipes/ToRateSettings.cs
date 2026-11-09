using System.ComponentModel.DataAnnotations;

namespace Pantry.Api.Modules.Recipes;

/// <summary>The <i>to rate</i> list, bound from <c>Recipes:ToRate</c>.</summary>
public sealed class ToRateSettings
{
    public const string Section = "Recipes:ToRate";
    public const int MaxDays = 365;

    /// <summary>How many days back, today included, unrated history entries are offered for rating.</summary>
    [Range(1, MaxDays)]
    public int Days { get; set; } = 14;
}
