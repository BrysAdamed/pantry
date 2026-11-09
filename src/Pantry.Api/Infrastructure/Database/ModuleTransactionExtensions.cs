using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Pantry.Api.Infrastructure.Database;

public static class ModuleTransactionExtensions
{
    /// <summary>
    /// Runs this context on another module's open connection and transaction, so the writes of both modules
    /// commit or roll back together (ADR 0015). Use a context created for this call only, not the scoped one.
    /// </summary>
    public static async Task JoinTransactionAsync(this DatabaseFacade database, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection
            ?? throw new ArgumentException("The transaction is already completed.", nameof(transaction));
        database.SetDbConnection(connection, contextOwnsConnection: false);
        await database.UseTransactionAsync(transaction, cancellationToken);
    }
}
