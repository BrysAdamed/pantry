using Pantry.Api.Modules.Household;
using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Modules.Recipes;

/// <summary>
/// The household dislikes a recipe warns about: its <i>never</i> ingredients first, then its <i>avoid</i> ones, each in
/// line order. Read from the household on every request, so a changed dislike changes the warnings without touching recipes.
/// </summary>
internal sealed class DislikeWarnings
{
    private readonly Dictionary<Guid, HouseholdDislike> _byIngredient;

    private DislikeWarnings(IEnumerable<HouseholdDislike> dislikes) =>
        _byIngredient = dislikes.ToDictionary(d => d.IngredientId);

    public static async Task<DislikeWarnings> LoadAsync(IHouseholdModule household, CancellationToken cancellationToken) =>
        new(await household.GetDislikesAsync(cancellationToken));

    public IReadOnlyList<Guid> NeverIngredientIds() =>
        _byIngredient.Values.Where(d => d.Kind == DislikeKind.Never).Select(d => d.IngredientId).ToList();

    public IReadOnlyList<HouseholdDislike> Of(Recipe recipe) =>
        recipe.OrderedLines()
            .Select(line => line.IngredientId)
            .Distinct()
            .Select(ingredientId => _byIngredient.GetValueOrDefault(ingredientId))
            .OfType<HouseholdDislike>()
            .OrderBy(dislike => dislike.Kind)
            .ToList();
}
