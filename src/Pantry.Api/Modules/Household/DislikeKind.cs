namespace Pantry.Api.Modules.Household;

/// <summary>How strongly the household dislikes an ingredient. The API and the database use <c>never</c> and <c>avoid</c>.</summary>
public enum DislikeKind
{
    /// <summary>An allergy or a hard no: agents do not propose recipes with it, recipes show a red warning.</summary>
    Never,

    /// <summary>Better not, but edible: agents choose it rarely and mark it.</summary>
    Avoid,
}
