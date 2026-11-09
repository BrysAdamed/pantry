using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Modules.Catalog.Features.Ingredients;
using Pantry.Api.Modules.Catalog.Features.Vocabulary;
using Pantry.Api.Modules.Catalog.Persistence;

namespace Pantry.Api.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        services.AddScoped<ICatalogModule, CatalogModuleContract>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        var catalog = api.MapGroup("/catalog");
        catalog.MapGetVocabulary();

        var ingredients = catalog.MapGroup("/ingredients");
        ingredients.MapGetIngredients();
        ingredients.MapGetIngredient();
        ingredients.MapCreateIngredient();
        ingredients.MapUpdateIngredient();
        ingredients.MapResolveIngredient();
        ingredients.MapConfirmIngredient();
        return api;
    }
}
