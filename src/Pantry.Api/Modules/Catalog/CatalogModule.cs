using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog.Features.Duplicates;
using Pantry.Api.Modules.Catalog.Features.Ingredients;
using Pantry.Api.Modules.Catalog.Features.Vocabulary;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        services.AddScoped<ICatalogModule, CatalogModuleContract>();
        services.AddOptions<PossibleDuplicateSettings>()
            .Bind(configuration.GetSection(PossibleDuplicateSettings.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IngredientReferences>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        var catalog = api.MapGroup("/catalog");
        catalog.MapGetVocabulary();
        catalog.MapGetPossibleDuplicates();
        catalog.MapMarkNotDuplicates();

        var ingredients = catalog.MapGroup("/ingredients");
        ingredients.MapGetIngredients();
        ingredients.MapGetIngredient();
        ingredients.MapCreateIngredient();
        ingredients.MapUpdateIngredient();
        ingredients.MapResolveIngredient();
        ingredients.MapConfirmIngredient();
        ingredients.MapMergeIngredients();
        ingredients.MapDeleteIngredient();
        return api;
    }
}
