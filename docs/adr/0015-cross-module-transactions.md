# 0015 Cross-module writes share the caller's transaction

**Status:** Accepted, 2026-11-09. Extends [0012](0012-module-persistence.md).

## Context
Each module has its own `DbContext` (0012), and each context opens its own connection from the shared `NpgsqlDataSource`. Some use cases write to two modules and must be all or nothing. The first one is the first run: Identity creates the admin and Household must create the admin's member (and, from PAN-22, the default meal slots). A second context does not join the first context's transaction on its own, and Npgsql has no distributed transactions.

Two options were considered:
- **Share the transaction.** The calling module passes its open `DbTransaction` through the callee's contract, and the callee runs a context on that connection and transaction.
- **Idempotent "ensure" after commit.** The callee exposes "ensure member for user", called after the caller commits and retried on the next start or request when it fails.

## Decision
- Cross-module writes that must be atomic share the caller's transaction. The contract method takes a `DbTransaction` (for example `IHouseholdModule.CompleteFirstRunAsync(admin, transaction)`).
- The callee creates a context for that call only, from its own `DbContextOptions<TContext>`, and calls `Database.JoinTransactionAsync(transaction)` (`Infrastructure/Database/ModuleTransactionExtensions.cs`). It never enlists the scoped context, which other code in the request may still use on its own connection.
- The caller owns the transaction: it begins, commits or rolls back. The callee only calls `SaveChangesAsync`.
- Reads across modules go through the contracts without a transaction.

## Consequences
- First run is atomic: if the household setup fails, no admin is created and the first run can be repeated. No reconciliation job is needed.
- Works only while all modules live in one database, which 0002 and 0012 already assume. A module that moves to another database must switch its cross-module writes to the idempotent ensure pattern.
- The contracts of modules that take part in such writes mention `DbTransaction`, a small leak of persistence into the interface, accepted for the few use cases that need it.
