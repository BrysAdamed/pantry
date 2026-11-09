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
src/Pantry.Api/Infrastructure/  cross-cutting host code (database, health, antiforgery, data protection, HTTP)
src/Pantry.Web/e2e/      Playwright end-to-end tests
tests/Pantry.Api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers Postgres)
docs/                    vision, architecture, glossary, ADRs
```

## Modules

Modules, each owning its tables, endpoints and MCP tools. `Identity`, `Household` and `Catalog` exist; the rest are planned:

| Module | Owns | Epic |
|---|---|---|
| `Identity` | users, passkeys, passwords, invites, access tokens | E3, E8 |
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

## Ingredient catalog

See [ADR 0019](adr/0019-ingredient-catalog.md). Categories (`IngredientCategory`) and units (`Unit`) are enums in `Modules/Catalog/`; their camelCase name (`CatalogCode`) is the code in the API, the database and the SPA labels `catalog.category.<code>` / `catalog.unit.<code>`. `Units` holds the base units and the generic conversions; `Units.TryConvert` applies an ingredient's own unit conversions (`catalog.ingredient_conversions`, set with the ingredient on `POST`/`PUT /api/catalog/ingredients`) before the generic ones, and `CatalogIngredient.TryConvertToBaseUnit` / `ICatalogModule.ConvertToBaseUnitAsync` expose that to other modules. Names and aliases of every ingredient live in `catalog.ingredient_names` under one unique comparison key (`IngredientName.KeyOf`: lower case, no diacritics), so a duplicate name answers 409 with the ingredient that holds it. `ICatalogModule.ResolveIngredientAsync` (REST `POST /api/catalog/ingredients/resolve`) returns the ingredient with a name or alias, or creates one that needs review with a base unit guessed from the unit used (`Units.GuessBaseUnit`); a parallel create of the same name loses on the unique key and returns the winner. It commits on its own, outside any caller transaction. `POST /api/catalog/ingredients/{id}/confirm` clears needs review. Other modules read ingredients through `ICatalogModule` and register an `IIngredientReferenceHandler` when they store ingredient ids, so delete and merge can count and move their references. Merge ([ADR 0020](adr/0020-merging-ingredients.md)) is `GET /api/catalog/ingredients/{target}/merge-preview?sourceId=` and `POST /api/catalog/ingredients/{target}/merge`: `IngredientMerge` plans it in the domain (aliases and the source's conversions to add or skip), `IngredientReferences` calls the handlers (ordered by module) inside the catalog transaction, and tests register a handler of the test module (`TestIngredientReferenceHandler`) to check moves and rollback. `DELETE /api/catalog/ingredients/{id}` deletes only an ingredient every handler counts zero references for; otherwise it answers 409 with `references` (module and count for each module that uses it), so the SPA can offer a merge.

## Authentication

See [ADR 0014](adr/0014-spa-authentication.md). Users sign in with a passkey or with a username and password ([ADR 0018](adr/0018-password-sign-in.md)); either *sign-in method* is enough and the last one cannot be removed. The password policy (12 to 128 characters, no character-type rules) and the lockout (15 minutes after 5 failures per user) are Identity options set in `Auth/PasswordConfiguration.cs`; username rules are an Identity user validator (`Auth/UserNameValidator.cs`). `POST /api/auth/sign-in/password` answers the same 401, after one password hash, for an unknown user, a wrong password and a locked account; password checks of one user are serialized by a per-user advisory lock, so parallel guesses all count. First run and links have `complete-with-password` endpoints next to the passkey ones, and `/api/me/password` sets, changes and removes the password; each change of a sign-in method in My account or by a recovery link signs out the user's other devices. The session is an HttpOnly cookie. Every `/api` endpoint requires a signed-in user; opt out with `.AllowAnonymous()`. Unsafe `/api` requests need the `X-XSRF-TOKEN` header, which Angular's `HttpClient` sends from the `XSRF-TOKEN` cookie. The SPA gets a fresh anonymous token from `GET /api/antiforgery` before signing in. Data Protection keys (cookie encryption) live in the `data_protection` schema ([ADR 0016](adr/0016-data-protection-keys.md)). Admins bring people in with invites and recovery links and manage users under the `admin` policy ([ADR 0017](adr/0017-passkey-links-and-user-administration.md)); sessions are rechecked against the user every minute (`Identity:Session:ValidationInterval`). Each user keeps their UI language and theme and manages their own passkeys and password under `/api/me` (*My account*); the SPA reads `/api/me` again on a 403 and when the app comes back into view, so a changed role shows without a reload. Passkey relying party ID and origins come from `Identity:Passkeys` (`appsettings.Development.json` for dev, `PASSKEY_*` in `.env` for Docker).

## Frontend

- Angular 21, zoneless, signals, standalone components, `httpResource` for reads.
- Tailwind CSS v4 and Spartan UI. Spartan's styled (helm) components are copied into `src/app/shared/ui/` and imported as `@spartan-ng/helm/<name>`. Add one with `npx ng g @spartan-ng/cli:ui <name>`. Design tokens come from E2.
- Angular CDK for drag and drop in the planner.
- Feature folders mirror backend modules or screen areas: `src/app/<feature>/` (for example `identity/` for sign-in, `account/` for My account, both served by the `Identity` module). App-wide services live in `src/app/core/`.
- The shell (`src/app/shell/`) frames every screen: an icon rail from the `md` breakpoint (768px) up, a header with sync state and avatar plus bottom tabs below it. Each destination (`planner`, `recipes`, `shopping`, `household`) is a lazy route that loads `src/app/<feature>/<feature>.routes.ts`; until its epic lands, that file shows the feature's empty state.
- Menus and popovers use Angular CDK overlays (ADR 0009) with a focus trap and Escape to close.
- i18n: PL and EN at runtime with Transloco ([ADR 0011](adr/0011-runtime-i18n.md)). Labels live in `public/i18n/{en,pl}.json`; templates use `*transloco="let t"`. Every new key goes into both files (a test enforces it). Numbers and dates use Angular's locale pipes with the UI language as the locale (`number: '1.0-2' : language()`); locale data for `pl` is registered in `provideI18n`. Plural forms are ICU messages in the label files (`{count, plural, one {…} few {…} many {…} other {…}}`, [ADR 0021](adr/0021-icu-plural-labels.md)).
- Quantities go through the `quantity` pipe (`catalog/quantity/`, `{{ quantity | quantity: language() }}`) or `QuantityFormatter`: grams and millilitres from 1000 up as kg and l, unit names from `catalog.unitAmount.<code>` in the right plural form, parts that could not be added up joined as `3 szt. + 300 g`.
- Choosing an ingredient always goes through `<app-ingredient-picker>` (`catalog/ingredient-picker/`): an ARIA combobox over `GET /api/catalog/ingredients?search=`, which marks the matched part and the matched alias, shows `taken` ingredients as not selectable and leaves `exclude`d ones out. With `allowNew` (on by default) it also offers a name the catalog does not have; the host keeps that `IngredientChoice` and turns it into an ingredient with `CatalogApi.ingredientOf(choice, unit)` (the resolve endpoint) when its form saves, so nothing is created for a form that is cancelled.
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
