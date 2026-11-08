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
tests/Pantry.Api.Tests/  xUnit integration tests (WebApplicationFactory, later Testcontainers)
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

## Frontend

- Angular 21, zoneless, signals, standalone components, `httpResource` for reads.
- Tailwind CSS v4 and Spartan UI (copy-in components). Design tokens come from E2.
- Angular CDK for drag and drop in the planner.
- Feature folders mirror backend modules: `src/app/<feature>/`.
- i18n: PL and EN.
- PWA: `@angular/service-worker`. Offline writes (shopping lists only) queue in IndexedDB.

## Development

```bash
docker compose up -d                      # Postgres only
dotnet run --project src/Pantry.Api       # API on http://localhost:5080
cd src/Pantry.Web && npm start            # SPA on http://localhost:4200, proxies /api and /mcp
```

Full stack in Docker:

```bash
docker compose --profile app up -d --build   # http://localhost:8080
```

## Configuration and secrets

- All configuration comes from `appsettings.json` defaults and environment variables.
- Secrets go in `.env` (git-ignored). `.env.example` lists every variable.
- The repo is public: no personal data, real hostnames, shop names or credentials in code, docs or tests.
