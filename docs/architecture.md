# Architecture

## Overview

```
 Phones / browsers ──HTTPS──┐
                            ▼
 Claude Desktop ──MCP──► Pantry.Api (.NET 10) ──► PostgreSQL
 (home computer)          ├─ /api/*   REST for the Angular app
                          ├─ /mcp     MCP server (Streamable HTTP)
                          ├─ /hubs/*  SignalR (live list sync)
                          └─ /*       Angular SPA (static files, PWA)
```

One container serves the API, the MCP endpoint and the built Angular app. Postgres runs next to it in Docker Compose. See the ADRs in [`adr/`](adr/) for the reasoning behind each choice.

## Repository layout

```
src/Pantry.Api/          .NET host: endpoints, modules, MCP, static SPA in wwwroot/
src/Pantry.Api/Modules/  one folder per module (see below)
src/Pantry.Web/          Angular 21 app
src/Pantry.Api/Infrastructure/  cross-cutting host code (database, health)
src/Pantry.Web/e2e/      Playwright end-to-end tests
tests/Pantry.Api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers Postgres)
docs/                    vision, architecture, glossary, ADRs
```

## Modules

Planned modules, each owning its tables, endpoints and MCP tools:

| Module | Owns | Epic |
|---|---|---|
| `Identity` | users, passkeys, invites, access tokens | E3, E8 |
| `Household` | members, portion factors, meal slots, dislikes, rules, content language | E3 |
| `Catalog` | ingredients, units, conversions, staples | E4 |
| `Recipes` | recipes, nutrition, ratings | E5 |
| `Planner` | plan entries, leftovers, history | E6 |
| `Shopping` | lists, items, orders, cart run reports, product memory | E7 |

Rules:

- A module lives in `Modules/<Name>/` and exposes one `Add<Name>Module` / `Map<Name>Endpoints` pair, called from `Program.cs`.
- Each module has its own EF Core `DbContext` and Postgres schema. No cross-module table access. Modules talk through small public interfaces (`I<Name>Module`) in-process.
- Endpoints are organised as vertical slices: request, handler, response in one file per use case. No mediator library.
- If a module grows too large, it can move to its own project without changing its contract.

## Persistence

See [ADR 0012](adr/0012-module-persistence.md). A module registers its context with `services.AddModuleDbContext<CatalogDbContext>("catalog")` and sets `HasDefaultSchema("catalog")` in `OnModelCreating`. Pending migrations of all registered modules run on startup.

A write that spans two modules and must be atomic shares the caller's transaction ([ADR 0015](adr/0015-cross-module-transactions.md)): the contract method takes the caller's `DbTransaction`, and the callee joins it with `Database.JoinTransactionAsync` on a context created for that call. First run uses it to create the admin and the admin's member together.

Add a migration (the `dotnet-ef` tool is pinned in `dotnet-tools.json`, run `dotnet tool restore` once):

```bash
dotnet ef migrations add <Name> --project src/Pantry.Api --context CatalogDbContext --output-dir Modules/Catalog/Persistence/Migrations
```

## Authentication

See [ADR 0014](adr/0014-spa-authentication.md). Users sign in with passkeys only; the session is an HttpOnly cookie. Every `/api` endpoint requires a signed-in user; opt out with `.AllowAnonymous()`. Unsafe `/api` requests need the `X-XSRF-TOKEN` header, which Angular's `HttpClient` sends from the `XSRF-TOKEN` cookie. Passkey relying party ID and origins come from `Identity:Passkeys` (`appsettings.Development.json` for dev, `PASSKEY_*` in `.env` for Docker).

## Frontend

- Angular 21, zoneless, signals, standalone components, `httpResource` for reads.
- Tailwind CSS v4 and Spartan UI. Spartan's styled (helm) components are copied into `src/app/shared/ui/` and imported as `@spartan-ng/helm/<name>`. Add one with `npx ng g @spartan-ng/cli:ui <name>`. Design tokens come from E2.
- Angular CDK for drag and drop in the planner.
- Feature folders mirror backend modules: `src/app/<feature>/`. App-wide services live in `src/app/core/`.
- The shell (`src/app/shell/`) frames every screen: an icon rail from the `md` breakpoint (768px) up, a header with sync state and avatar plus bottom tabs below it. Each destination (`planner`, `recipes`, `shopping`, `household`) is a lazy route that loads `src/app/<feature>/<feature>.routes.ts`; until its epic lands, that file shows the feature's empty state.
- Menus and popovers use Angular CDK overlays (ADR 0009) with a focus trap and Escape to close.
- i18n: PL and EN at runtime with Transloco ([ADR 0011](adr/0011-runtime-i18n.md)). Labels live in `public/i18n/{en,pl}.json`; templates use `*transloco="let t"`. Every new key goes into both files (a test enforces it).
- PWA: `@angular/service-worker`. Offline writes (shopping lists only) queue in IndexedDB.

## Development

```bash
docker compose up -d                      # Postgres only
dotnet run --project src/Pantry.Api       # API on http://localhost:5080
cd src/Pantry.Web && npm start            # SPA on http://localhost:4200, proxies /api and /mcp
```

The development connection string in `appsettings.Development.json` matches `.env.example`. If you change the password in `.env`, override it with `dotnet user-secrets set ConnectionStrings:Pantry "<connection string>" --project src/Pantry.Api`.

Tests:

```bash
dotnet test                                        # needs Docker (Testcontainers starts Postgres)
cd src/Pantry.Web && npx ng test --watch=false     # Vitest unit tests
cd src/Pantry.Web && npm run e2e                   # Playwright, starts ng serve, API is mocked
```

Run `npx playwright install chromium` once before the first e2e run.

Full stack in Docker:

```bash
docker compose --profile app up -d --build   # http://localhost:8080
```

## Configuration and secrets

- All configuration comes from `appsettings.json` defaults and environment variables.
- Secrets go in `.env` (git-ignored). `.env.example` lists every variable.
- The repo is public: no personal data, real hostnames, shop names or credentials in code, docs or tests.
