using Microsoft.Extensions.DependencyInjection.Extensions;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog;
using Pantry.Api.Modules.Recipes.Features.History;
using Pantry.Api.Modules.Recipes.Features.Ratings;
using Pantry.Api.Modules.Recipes.Features.Recipes;
using Pantry.Api.Modules.Recipes.Features.Tags;
using Pantry.Api.Modules.Recipes.Persistence;

namespace Pantry.Api.Modules.Recipes;

public static class RecipesModule
{
    public static IServiceCollection AddRecipesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<RecipesDbContext>(RecipesDbContext.Schema);
        services.AddScoped<IRecipesModule, RecipesModuleContract>();
        services.AddScoped<IIngredientReferenceHandler, RecipeIngredientReferences>();
        services.AddOptions<ToRateSettings>()
            .Bind(configuration.GetSection(ToRateSettings.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapRecipesEndpoints(this IEndpointRouteBuilder api)
    {
        var recipes = api.MapGroup("/recipes");
        recipes.MapGetRecipes();
        recipes.MapGetRecipe();
        recipes.MapCreateRecipe();
        recipes.MapUpdateRecipe();
        recipes.MapArchiveRecipe();
        recipes.MapGetTags();
        recipes.MapGetRecipeHistory();
        recipes.MapGetToRate();
        recipes.MapRateHistoryEntry();
        recipes.MapSetMakeAgain();
        return api;
    }
}
