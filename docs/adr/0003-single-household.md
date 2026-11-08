# 0003 One household per deployment

**Status:** Accepted, 2026-11-08

## Context
The app is for one family. Others may fork and self-host it. Multi-tenancy would add a tenant key to every table and query.

## Decision
A deployment serves exactly one household. Household configuration (members, portion factors, meal slots, dislikes, rules, content language) is a singleton. All users belong to it.

## Consequences
- Simpler data model and authorization: every signed-in user sees everything.
- Another family runs its own instance.
- Production starts with an empty database. A demo seed may exist only for a demo instance.
