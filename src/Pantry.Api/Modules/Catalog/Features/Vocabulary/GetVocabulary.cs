namespace Pantry.Api.Modules.Catalog.Features.Vocabulary;

/// <summary>
/// The built-in categories and units in their fixed order, with the base units and the generic conversions. The SPA
/// labels them with <c>catalog.category.&lt;code&gt;</c> and <c>catalog.unit.&lt;code&gt;</c>.
/// </summary>
public static class GetVocabulary
{
    public static RouteHandlerBuilder MapGetVocabulary(this IEndpointRouteBuilder catalog) => catalog.MapGet("/vocabulary", Handle);

    private static VocabularyResponse Handle() => new(
        IngredientCategories.Ordered,
        IngredientCategories.Default,
        Units.BaseUnits,
        Units.Ordered.Select(unit => new UnitResponse(unit, Units.GenericConversionOf(unit))).ToList());
}

public sealed record VocabularyResponse(
    IReadOnlyList<IngredientCategory> Categories,
    IngredientCategory DefaultCategory,
    IReadOnlyList<Unit> BaseUnits,
    IReadOnlyList<UnitResponse> Units);

/// <summary>A unit and, when it holds for every ingredient, its conversion into a base unit.</summary>
public sealed record UnitResponse(Unit Code, GenericConversion? GenericConversion);
