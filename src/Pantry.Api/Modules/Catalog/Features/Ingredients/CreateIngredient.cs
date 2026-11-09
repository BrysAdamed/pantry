using Microsoft.AspNetCore.Http.HttpResults;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>Adds an ingredient a person typed in. It does not need review; the category defaults to <c>other</c>.</summary>
public static class CreateIngredient
{
    public static RouteHandlerBuilder MapCreateIngredient(this IEndpointRouteBuilder ingredients) => ingredients.MapPost("/", HandleAsync);

    private static async Task<Results<Created<IngredientResponse>, ValidationProblem, ProblemHttpResult>> HandleAsync(
        CreateIngredientRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        IngredientValidation.ValidateNames(request.Name, request.Aliases, errors, out var name, out var aliases);
        if (request.BaseUnit is not { } baseUnit || !Units.IsBaseUnit(baseUnit))
        {
            errors[nameof(request.BaseUnit)] = [$"Base unit must be one of: {string.Join(", ", Units.BaseUnits.Select(CatalogCode.Of))}."];
            return TypedResults.ValidationProblem(errors);
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var ingredient = Ingredient.Create(name, aliases, baseUnit, request.Category ?? IngredientCategories.Default, request.IsStaple);
        db.Ingredients.Add(ingredient);
        if (await IngredientValidation.SaveAsync(db, ingredient, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return TypedResults.Created($"/api/catalog/ingredients/{ingredient.Id}", IngredientResponse.From(ingredient));
    }
}

public sealed record CreateIngredientRequest(
    string? Name,
    IReadOnlyList<string?>? Aliases,
    Unit? BaseUnit,
    IngredientCategory? Category,
    bool IsStaple);
