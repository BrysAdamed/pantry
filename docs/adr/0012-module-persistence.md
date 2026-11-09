# 0012 Module persistence: schema per module, migrations on startup

**Status:** Accepted, 2026-11-09

## Context
Each module owns its tables (see 0002). The app runs as one container on a homelab, upgraded by pulling a new image; there is no separate deployment pipeline to run migrations.

## Decision
- A module registers its `DbContext` with `services.AddModuleDbContext<TContext>("<schema>")`. The context sets `HasDefaultSchema("<schema>")`.
- Each module keeps its own migrations in `Modules/<Name>/Persistence/Migrations` and its own history table `<schema>.__ef_migrations_history`.
- All module contexts share one `NpgsqlDataSource` (connection string `ConnectionStrings__Pantry`).
- Tables and columns use snake_case (`EFCore.NamingConventions`).
- The app applies pending migrations of every registered module on startup, before it serves requests.
- `/api/health` checks the database and returns 503 when it is unreachable.

## Consequences
- Upgrading is "pull and restart". A failed migration stops the app instead of running against a half-migrated schema.
- Modules can be migrated, reset or moved to their own project independently.
- Startup migration assumes a single app instance, which matches 0003.
- Back up the database before upgrading (E10).
