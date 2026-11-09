# 0016 Data Protection keys in Postgres

**Status:** Accepted, 2026-11-09

## Context
The session cookie, the passkey ceremony cookie and the antiforgery tokens are encrypted with ASP.NET Core Data Protection (ADR 0014). By default the key ring lives in the container's file system, so recreating the container on upgrade ("pull and restart", ADR 0012) would sign everybody out and break open forms. The deployment already has one durable store: Postgres.

## Decision
- The key ring is stored in Postgres with `PersistKeysToDbContext`, in its own `data_protection` schema registered through `AddModuleDbContext`, so it is migrated on startup like a module.
- The application name is fixed (`Pantry`), so keys do not depend on the install path.
- Keys are not encrypted at rest. Whoever can read the database can already read all household data; backups (E10) contain the keys and must be treated as secrets.

## Consequences
- Sessions survive container recreation and upgrades; no extra volume is needed.
- Restoring a backup restores the keys too, so sessions issued before the backup stay valid.
- Rotating keys (default every 90 days) happens in the database; old keys stay for decrypting existing cookies.
