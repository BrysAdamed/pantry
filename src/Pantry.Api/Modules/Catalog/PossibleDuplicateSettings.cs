using System.ComponentModel.DataAnnotations;

namespace Pantry.Api.Modules.Catalog;

/// <summary>Possible duplicates, bound from <c>Catalog:PossibleDuplicates</c>.</summary>
public sealed class PossibleDuplicateSettings
{
    public const string Section = "Catalog:PossibleDuplicates";

    /// <summary>
    /// The lowest trigram similarity (0 to 1) between a name or alias of one ingredient and one of another for the pair
    /// to be a possible duplicate. Lower finds more pairs and more false ones.
    /// </summary>
    [Range(0.1, 1.0)]
    public double Threshold { get; set; } = 0.4;
}
