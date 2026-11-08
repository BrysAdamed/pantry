# Pantry (Spiżarnia)

A self-hosted meal planner and shared shopping list for one household, with an MCP server so AI agents can plan the week and report what they put in your online shop cart.

> Status: early development. Nothing usable yet; see the roadmap below.

## What it does (planned)

- **Recipe library** that remembers what you cooked, how you rated it, and what you never want to see again.
- **Calendar planner**: drag dishes in and out of meal slots, leave empty slots for an agent to fill, cook once and eat twice.
- **Shopping lists** computed from the plan (no LLM arithmetic), plus shared general lists with live sync.
- **MCP server** for Claude Desktop or any MCP client: read preferences and history, propose plans, take a shopping order, store the cart run report.
- **Offline PWA**: the shopping list works in the shop without signal.

Read the [vision](docs/vision.md) for the why.

## Stack

.NET 10 · Angular 21 · PostgreSQL · Tailwind CSS + Spartan UI · Docker Compose

## Getting started

Requirements: .NET 10 SDK, Node 24, Docker.

```bash
cp .env.example .env                      # then edit the password
docker compose up -d                      # Postgres
dotnet run --project src/Pantry.Api       # API on http://localhost:5080
cd src/Pantry.Web && npm install && npm start   # app on http://localhost:4200
```

Full stack in Docker: `docker compose --profile app up -d --build`, then open http://localhost:8080.

Tests:

```bash
dotnet test
cd src/Pantry.Web && npx ng test --watch=false
```

## Docs

- [Vision and idea log](docs/vision.md)
- [Architecture](docs/architecture.md)
- [Glossary](docs/glossary.md)
- [Architecture decision records](docs/adr/README.md)

## Roadmap

| Epic | |
|---|---|
| E1 | Foundation: scaffold, CI, docs, i18n |
| E2 | Design: visual direction, tokens, prototypes |
| E3 | Identity and household (passkeys, members, meal slots, dislikes) |
| E4 | Ingredient catalog and units |
| E5 | Recipes, nutrition, ratings, history |
| E6 | Meal planner calendar |
| E7 | Shopping lists, sharing, cart run report |
| E8 | MCP server and agent skills |
| E9 | Offline-first PWA |
| E10 | Deployment and ops (homelab) |

## License

[MIT](LICENSE)
