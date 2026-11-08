# 0001 Stack: .NET 10, Angular 21, PostgreSQL

**Status:** Accepted, 2026-11-08

## Context
A small self-hosted app for one household, maintained by one developer who knows .NET and Angular well. It must run on a homelab machine under Docker.

## Decision
- Backend: .NET 10 (LTS), ASP.NET Core minimal APIs, EF Core with Npgsql.
- Frontend: Angular 21, zoneless, signals.
- Database: PostgreSQL.
- Tests: xUnit with WebApplicationFactory and Testcontainers; Vitest for the frontend; Playwright for end-to-end.

## Consequences
- Familiar stack, fast to build. .NET 10 brings passkey support in Identity (see 0004) and has an official MCP SDK (see 0005).
- Postgres gives JSONB for loosely structured data (nutrition, agent metadata) without a second store.
