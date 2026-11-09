using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantry.Api.Modules.Catalog.Domain;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog.Features.Ingredients;

/// <summary>
/// Finds the ingredient with a name or alias, or creates it flagged as needing review (ADR 0019). Answers 200 with the
/// existing ingredient or 201 with the new one.
/// </summary>
public static class ResolveIngredient
{
    private const int MaxAttempts = 3;

    public static RouteHandlerBuilder MapResolveIngredient(this IEndpointRouteBuilder ingredients) => ingredients.MapPost("/resolve", HandleAsync);

    /// <summary>
    /// The ingredient known by <paramref name="name"/>, created when there is none. A create that loses a race with a
    /// parallel resolve of the same name is dropped and the winner's ingredient is returned.
    /// </summary>
    internal static async Task<(Ingredient Ingredient, bool Created)> ResolveAsync(
        CatalogDbContext db, string name, Unit? unitUsed, CancellationToken cancellationToken)
    {
        var key = IngredientName.KeyOf(name);
        for (var attempt = 1; ; attempt++)
        {
            var existing = await db.Ingredients.SingleOrDefaultAsync(i => i.Names.Any(n => n.Key == key), cancellationToken);
            if (existing is not null)
            {
                return (existing, false);
            }

            var ingredient = Ingredient.CreateFromUnknownName(name, unitUsed);
            db.Ingredients.Add(ingredient);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return (ingredient, true);
            }
            catch (DbUpdateException exception) when (CatalogDbContext.IsDuplicateName(exception) && attempt < MaxAttempts)
            {
                Detach(db, ingredient);
            }
        }
    }

    private static async Task<Results<Ok<IngredientResponse>, Created<IngredientResponse>, ValidationProblem>> HandleAsync(
        ResolveIngredientRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var name = IngredientName.Clean(request.Name ?? string.Empty);
        if (!Ingredient.IsValidName(name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = [$"Name must have 1 to {Ingredient.NameMaxLength} characters."],
            });
        }

        var (ingredient, created) = await ResolveAsync(db, name, request.Unit, cancellationToken);
        var response = IngredientResponse.From(ingredient);
        return created
            ? TypedResults.Created($"/api/catalog/ingredients/{ingredient.Id}", response)
            : TypedResults.Ok(response);
    }

    private static void Detach(CatalogDbContext db, Ingredient ingredient)
    {
        foreach (var entry in db.ChangeTracker.Entries()
                     .Where(e => ReferenceEquals(e.Entity, ingredient) || e.Entity is IngredientName n && ingredient.Names.Contains(n))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}

/// <summary><c>Unit</c> is the unit the name was used with; it only guesses the base unit of a new ingredient.</summary>
public sealed record ResolveIngredientRequest(string? Name, Unit? Unit);
