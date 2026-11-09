using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Domain;

namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>
/// Turns a <see cref="RecipeRequest"/> into <see cref="RecipeContent"/>: checks and cleans every field, then resolves the
/// ingredients. Ingredients named but not in the catalog are created only once the whole request is valid, because the
/// catalog commits them on its own (ADR 0019).
/// </summary>
internal static class RecipeInput
{
    public static async Task<(RecipeContent? Content, Dictionary<string, string[]> Errors)> ToContentAsync(
        RecipeRequest request, bool currentMakeAgain, ICatalogModule catalog, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        if (!RecipeContent.IsValidName(name))
        {
            errors[nameof(request.Name)] = [$"Name must have 1 to {RecipeContent.NameMaxLength} characters."];
        }

        if (request.Servings is not { } servings || !RecipeContent.IsValidServings(servings))
        {
            servings = 0;
            errors[nameof(request.Servings)] = [$"Servings must be between 1 and {RecipeContent.MaxServings}."];
        }

        if (!RecipeContent.IsValidPrepTime(request.PrepTimeMinutes))
        {
            errors[nameof(request.PrepTimeMinutes)] = [$"Prep time must be between 1 and {RecipeContent.MaxPrepTimeMinutes} minutes."];
        }

        var sourceUrl = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim();
        if (sourceUrl is not null && !RecipeContent.IsValidSourceUrl(sourceUrl))
        {
            errors[nameof(request.SourceUrl)] = [$"Source URL must be an http or https address of at most {RecipeContent.SourceUrlMaxLength} characters."];
        }

        if (!RecipeContent.IsValidCooldown(request.CooldownDays))
        {
            errors[nameof(request.CooldownDays)] = [$"Cooldown must be between 0 and {RecipeContent.MaxCooldownDays} days."];
        }

        var tags = CleanTags(request.Tags, errors);
        var steps = CleanSteps(request.Steps, errors);
        var nutrition = new Nutrition(request.Nutrition?.Kcal, request.Nutrition?.Protein, request.Nutrition?.Fat, request.Nutrition?.Carbs);
        if (!new[] { nutrition.Kcal, nutrition.Protein, nutrition.Fat, nutrition.Carbs }.All(Nutrition.IsValidValue))
        {
            errors[nameof(request.Nutrition)] = [$"Nutrition values must be between 0 and {Nutrition.MaxValue}."];
        }

        var lines = CleanLines(request, errors);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var ingredientIds = await ResolveIngredientsAsync(lines, catalog, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var content = new RecipeContent(
            name,
            servings,
            request.PrepTimeMinutes,
            sourceUrl,
            tags,
            steps,
            nutrition,
            request.MakeAgain ?? currentMakeAgain,
            request.CooldownDays,
            lines.Select((line, i) => new RecipeLine(line.Section, ingredientIds[i], line.Quantity, line.Unit, line.Note)).ToList());
        return (content, errors);
    }

    private static List<string> CleanTags(IReadOnlyList<string?>? tags, Dictionary<string, string[]> errors)
    {
        var clean = (tags ?? []).Select(t => RecipeTag.Normalize(t ?? string.Empty)).Distinct().ToList();
        if (!clean.All(RecipeTag.IsValid))
        {
            errors[nameof(RecipeRequest.Tags)] = [$"Each tag must have 1 to {RecipeTag.MaxLength} characters."];
        }
        else if (clean.Count > RecipeContent.MaxTags)
        {
            errors[nameof(RecipeRequest.Tags)] = [$"A recipe can have at most {RecipeContent.MaxTags} tags."];
        }

        return clean;
    }

    private static List<string> CleanSteps(IReadOnlyList<string?>? steps, Dictionary<string, string[]> errors)
    {
        var clean = (steps ?? []).Select(s => s?.Trim() ?? string.Empty).ToList();
        if (clean.Count > RecipeContent.MaxSteps)
        {
            errors[nameof(RecipeRequest.Steps)] = [$"A recipe can have at most {RecipeContent.MaxSteps} steps."];
        }

        for (var i = 0; i < clean.Count; i++)
        {
            if (!RecipeStep.IsValidText(clean[i]))
            {
                errors[$"{nameof(RecipeRequest.Steps)}[{i}]"] = [$"A step must have 1 to {RecipeStep.TextMaxLength} characters."];
            }
        }

        return clean;
    }

    private static List<CleanLine> CleanLines(RecipeRequest request, Dictionary<string, string[]> errors)
    {
        var lines = new List<CleanLine>();
        AddLines(request.Lines, null, nameof(RecipeRequest.Lines), lines, errors);

        var sectionKeys = new HashSet<string>();
        var sections = request.Sections ?? [];
        for (var s = 0; s < sections.Count; s++)
        {
            var path = $"{nameof(RecipeRequest.Sections)}[{s}]";
            var section = sections[s];
            var sectionName = section?.Name?.Trim() ?? string.Empty;
            if (!RecipeContent.IsValidSectionName(sectionName))
            {
                errors[$"{path}.Name"] = [$"A section name must have 1 to {RecipeContent.SectionNameMaxLength} characters."];
            }
            else if (!sectionKeys.Add(RecipeContent.SectionKey(sectionName)))
            {
                errors[$"{path}.Name"] = ["Section names must differ from each other."];
            }

            if (section?.Lines is not { Count: > 0 })
            {
                errors[$"{path}.Lines"] = ["A section needs at least one ingredient line."];
                continue;
            }

            AddLines(section.Lines, sectionName, $"{path}.Lines", lines, errors);
        }

        if (lines.Count > RecipeContent.MaxLines)
        {
            errors[nameof(RecipeRequest.Lines)] = [$"A recipe can have at most {RecipeContent.MaxLines} ingredient lines."];
        }

        return lines;
    }

    private static void AddLines(
        IReadOnlyList<RecipeLineRequest?>? requests, string? section, string path, List<CleanLine> lines, Dictionary<string, string[]> errors)
    {
        var all = requests ?? [];
        for (var i = 0; i < all.Count; i++)
        {
            var linePath = $"{path}[{i}]";
            var request = all[i];
            var ingredientName = string.IsNullOrWhiteSpace(request?.IngredientName) ? null : request.IngredientName.Trim();
            if ((request?.IngredientId is null) == (ingredientName is null))
            {
                errors[$"{linePath}.Ingredient"] = ["A line needs either an ingredient id or an ingredient name."];
            }
            else if (ingredientName is not null && ingredientName.Length > CatalogIngredient.NameMaxLength)
            {
                errors[$"{linePath}.IngredientName"] = [$"An ingredient name must have at most {CatalogIngredient.NameMaxLength} characters."];
            }

            if (request?.Quantity is { } quantity && !RecipeQuantities.IsValidQuantity(quantity))
            {
                errors[$"{linePath}.Quantity"] =
                [
                    $"Quantity must be between {RecipeQuantities.MinQuantity} and {RecipeQuantities.MaxQuantity} " +
                    $"with at most {RecipeQuantities.QuantityScale} decimal places.",
                ];
            }

            if ((request?.Quantity is null) != (request?.Unit is null))
            {
                errors[$"{linePath}.Unit"] = ["A quantity needs a unit, and a unit needs a quantity; leave both out for to taste."];
            }

            var note = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim();
            if (note is not null && !RecipeIngredientLine.IsValidNote(note))
            {
                errors[$"{linePath}.Note"] = [$"A note must have at most {RecipeIngredientLine.NoteMaxLength} characters."];
            }

            lines.Add(new CleanLine(linePath, section, request?.IngredientId, ingredientName, request?.Quantity, request?.Unit, note));
        }
    }

    private static async Task<Guid[]> ResolveIngredientsAsync(
        IReadOnlyList<CleanLine> lines, ICatalogModule catalog, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        var requestedIds = lines.Where(l => l.IngredientId is not null).Select(l => l.IngredientId!.Value).Distinct().ToList();
        var known = (await catalog.GetIngredientsAsync(requestedIds, cancellationToken)).Select(i => i.Id).ToHashSet();
        foreach (var line in lines.Where(l => l.IngredientId is { } id && !known.Contains(id)))
        {
            errors[$"{line.Path}.IngredientId"] = ["The ingredient does not exist."];
        }

        var ids = new Guid[lines.Count];
        if (errors.Count > 0)
        {
            return ids;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            ids[i] = lines[i].IngredientId
                ?? (await catalog.ResolveIngredientAsync(lines[i].IngredientName!, lines[i].Unit, cancellationToken)).Ingredient.Id;
        }

        return ids;
    }

    private sealed record CleanLine(string Path, string? Section, Guid? IngredientId, string? IngredientName, decimal? Quantity, Unit? Unit, string? Note);
}
