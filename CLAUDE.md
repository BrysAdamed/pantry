# Pantry - notes for agents

Self-hosted household meal planner and shopping list with an MCP server. Read `docs/vision.md`, `docs/architecture.md` and `docs/glossary.md` before starting a ticket; check `docs/adr/` before changing anything structural.

## Hard rules

- **Public repo.** Never commit personal data: real names, addresses, hostnames, IPs, shop names, account details, real recipes or shopping history from the household. Use generic examples. Secrets only in `.env` (git-ignored).
- Use glossary terms in code, API and UI. Add a term to `docs/glossary.md` when you introduce one.
- A structural decision gets a new ADR in `docs/adr/` (next number) and a row in `docs/adr/README.md`.
- No LLM calls inside the app. Agent features go through the MCP server.
- No hardcoded UI strings; every label in PL and EN.

## Work tracking

Tickets live in the ticketing app (MCP server `ticketing`), project key `PAN`. Epics: PAN-1 to PAN-10.

## Layout and commands

- `src/Pantry.Api` - .NET 10 host. Modules in `Modules/<Name>/`. Serves the SPA from `wwwroot` in production.
- `src/Pantry.Web` - Angular 21 (zoneless, signals), Tailwind v4, Spartan UI.
- `tests/Pantry.Api.Tests` - xUnit integration tests.

```bash
docker compose up -d                       # Postgres
dotnet run --project src/Pantry.Api        # http://localhost:5080
cd src/Pantry.Web && npm start             # http://localhost:4200 (proxies /api, /mcp)
dotnet test
cd src/Pantry.Web && npx ng test --watch=false
```
