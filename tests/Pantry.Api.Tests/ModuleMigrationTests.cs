using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pantry.Api.Infrastructure.Database;
using Pantry.Api.Tests.Infrastructure;
using Pantry.Api.Tests.Infrastructure.TestModule;

namespace Pantry.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ModuleMigrationTests(PantryApiFactory factory)
{
    [Fact]
    public async Task Startup_migrates_module_into_its_own_schema()
    {
        // Arrange
        _ = factory.Services;
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();

        // Act
        var tables = await QueryTablesAsync(connection, TestModuleDbContext.Schema);

        // Assert
        Assert.Contains("test_items", tables);
        Assert.Contains(DatabaseServiceCollectionExtensions.MigrationsHistoryTable, tables);
        Assert.DoesNotContain(DatabaseServiceCollectionExtensions.MigrationsHistoryTable, await QueryTablesAsync(connection, "public"));
    }

    [Fact]
    public async Task Module_context_reads_and_writes_with_snake_case_columns()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TestModuleDbContext>();
        var item = new TestItem { Id = Guid.NewGuid(), DisplayName = "Example item" };

        // Act
        context.TestItems.Add(item);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.TestItems.SingleAsync(i => i.Id == item.Id);

        // Assert
        Assert.Equal("Example item", stored.DisplayName);
    }

    private static async Task<List<string>> QueryTablesAsync(NpgsqlConnection connection, string schema)
    {
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema", connection);
        command.Parameters.AddWithValue("schema", schema);
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }
}
