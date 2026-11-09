using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// Renames an ingredient, replaces its aliases, sets its category and staple flag and, when given, its base unit and
/// unit conversions. A new base unit removes the conversions; the request's conversions are in the base unit it ends
/// up with, and leaving them out keeps the current ones only while the base unit stays.
/// </summary>
public static class UpdateIngredient
{
    public static RouteHandlerBuilder MapUpdateIngredient(this IEndpointRouteBuilder ingredients) => ingredients.MapPut("/{id:guid}", HandleAsync);

    private static async Task<Results<Ok<IngredientResponse>, NotFound, ValidationProblem, ProblemHttpResult>> HandleAsync(
        Guid id, UpdateIngredientRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        IngredientValidation.ValidateNames(request.Name, request.Aliases, errors, out var name, out var aliases);
        if (request.Category is not { } category)
        {
            errors[nameof(request.Category)] = [$"Category must be one of: {string.Join(", ", IngredientCategories.Ordered.Select(CatalogCode.Of))}."];
            return TypedResults.ValidationProblem(errors);
        }

        if (request.BaseUnit is { } requestedBaseUnit && !Units.IsBaseUnit(requestedBaseUnit))
        {
            errors[nameof(request.BaseUnit)] = [$"Base unit must be one of: {string.Join(", ", Units.BaseUnits.Select(CatalogCode.Of))}."];
            return TypedResults.ValidationProblem(errors);
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var ingredient = await db.Ingredients.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (ingredient is null)
        {
            return TypedResults.NotFound();
        }

        var baseUnit = request.BaseUnit ?? ingredient.BaseUnit;
        IReadOnlyList<(Unit Unit, decimal Amount)>? conversions = null;
        if (request.Conversions is not null)
        {
            IngredientValidation.ValidateConversions(baseUnit, request.Conversions, errors, out var valid);
            if (errors.Count > 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            conversions = valid;
        }

        ingredient.SetNames(name, aliases);
        ingredient.Category = category;
        ingredient.IsStaple = request.IsStaple;
        ingredient.SetBaseUnit(baseUnit);
        if (conversions is not null)
        {
            ingredient.SetConversions(conversions);
        }

        if (await IngredientValidation.SaveAsync(db, ingredient, cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return TypedResults.Ok(IngredientResponse.From(ingredient));
    }
}

public sealed record UpdateIngredientRequest(
    string? Name,
    IReadOnlyList<string?>? Aliases,
    IngredientCategory? Category,
    bool IsStaple,
    Unit? BaseUnit = null,
    IReadOnlyList<ConversionRequest?>? Conversions = null);
