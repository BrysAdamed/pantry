using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Infrastructure;

namespace Pantry.Api.Tests.Recipes;

/// <summary>Writes history the way the Planner will: through <see cref="IRecipesModule"/> inside a committed transaction.</summary>
public static class RecipeHistorySeeding
{
    public static Task RecordEatenAsync(this PantryApiFactory factory, EatenPlanEntry entry) =>
        factory.InRecipesTransactionAsync((recipes, transaction) => recipes.RecordEatenAsync(entry, transaction));

    public static Task RecordUneatenAsync(this PantryApiFactory factory, Guid planEntryId) =>
        factory.InRecipesTransactionAsync((recipes, transaction) => recipes.RecordUneatenAsync(planEntryId, transaction));

    /// <summary>Records an eaten plan entry and returns the id of its history entry.</summary>
    public static async Task<Guid> SeedHistoryEntryAsync(this PantryApiFactory factory, Guid recipeId, DateOnly date)
    {
        var planEntryId = Guid.NewGuid();
        await factory.RecordEatenAsync(new EatenPlanEntry(planEntryId, recipeId, date, Guid.NewGuid(), IsLeftover: false));
        await using var scope = factory.Services.CreateAsyncScope();
        var history = await scope.ServiceProvider.GetRequiredService<IRecipesModule>().GetHistoryAsync(recipeId);
        return history.Single(h => h.PlanEntryId == planEntryId).Id;
    }

    private static async Task InRecipesTransactionAsync(this PantryApiFactory factory, Func<IRecipesModule, NpgsqlTransaction, Task> write)
    {
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        await write(scope.ServiceProvider.GetRequiredService<IRecipesModule>(), transaction);
        await transaction.CommitAsync();
    }
}
