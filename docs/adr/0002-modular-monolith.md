# 0002 Modular monolith, one container

**Status:** Accepted, 2026-11-08

## Context
Low traffic, two or three users, one deployment target. Operational simplicity matters more than independent scaling.

## Decision
- One ASP.NET Core host (`Pantry.Api`) serves REST (`/api`), MCP (`/mcp`), SignalR and the built Angular SPA from `wwwroot`.
- Code is split into modules (Identity, Household, Catalog, Recipes, Planner, Shopping) as folders with their own `DbContext` and Postgres schema. Modules call each other only through small public interfaces.
- Vertical slices for endpoints. No mediator or CQRS framework.
- The Docker image is built in one multi-stage Dockerfile (Node build, then .NET publish).

## Consequences
- One image to deploy and back up. No CORS in production.
- Module boundaries keep the option of splitting projects later.
- Development uses `ng serve` with a proxy to the API.
