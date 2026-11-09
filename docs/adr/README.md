# Architecture Decision Records

Short records of decisions that shape the project. Format: context, decision, consequences. A new decision that changes an old one gets a new ADR and marks the old one *Superseded*.

| # | Decision | Status |
|---|---|---|
| [0001](0001-stack.md) | .NET 10, Angular 21, PostgreSQL | Accepted |
| [0002](0002-modular-monolith.md) | Modular monolith, one container serves API, MCP and SPA | Accepted |
| [0003](0003-single-household.md) | One household per deployment | Accepted |
| [0004](0004-passkeys.md) | ASP.NET Identity with passkeys, first-run admin and invites | Accepted (amended by 0018) |
| [0005](0005-mcp-no-in-app-agents.md) | MCP server in the app, no in-app agents, cart run stays in Claude | Accepted |
| [0006](0006-private-network-https.md) | No public exposure; HTTPS on a private hostname (choice deferred) | Accepted (TLS option open) |
| [0007](0007-offline-scope.md) | Offline: read everything, write shopping lists only | Accepted |
| [0008](0008-deterministic-shopping-list.md) | Shopping list computed in code with base units | Accepted |
| [0009](0009-ui-library.md) | Tailwind + Spartan UI instead of PrimeNG | Accepted |
| [0010](0010-languages.md) | English code and docs, PL/EN UI, household content language | Accepted |
| [0011](0011-runtime-i18n.md) | Runtime UI translations with Transloco | Accepted |
| [0012](0012-module-persistence.md) | Schema per module, migrations on startup | Accepted |
| [0013](0013-visual-direction-and-tokens.md) | Konfitura visual direction, `light-dark()` tokens, system theme with override | Accepted |
| [0014](0014-spa-authentication.md) | SPA authentication: Identity passkeys over JSON, cookie session, antiforgery header | Accepted (amended by 0018) |
| [0015](0015-cross-module-transactions.md) | Cross-module writes share the caller's transaction | Accepted |
| [0016](0016-data-protection-keys.md) | Data Protection keys in Postgres, so sessions survive container recreation | Accepted |
| [0017](0017-passkey-links-and-user-administration.md) | Invites and recovery links as hashed single-use passkey links; serialized user administration; sessions rechecked every minute | Accepted (amended by 0018) |
| [0018](0018-password-sign-in.md) | Password sign-in with a username as an alternative to passkeys; lockout; the last sign-in method stays | Accepted |
| [0019](0019-ingredient-catalog.md) | Ingredient catalog: fixed categories and units, aliases instead of translations, auto-created ingredients need review, merge through reference handlers | Accepted |
| [0020](0020-merging-ingredients.md) | Merging ingredients: target wins, conversions copied only between equal base units, alias limit holds, one transaction with rows locked in id order | Accepted |
| [0021](0021-possible-duplicates.md) | Possible duplicates: `pg_trgm` similarity of name keys with a configurable threshold, not duplicates stored per pair and removed with either ingredient | Accepted |
