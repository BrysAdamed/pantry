# 0009 Tailwind CSS and Spartan UI

**Status:** Accepted, 2026-11-08

## Context
The UI should look modern and friendly, not like an enterprise admin panel, and work well on phones. PrimeNG now has a paid tier. The project is open source.

## Decision
- Tailwind CSS v4 for styling and design tokens.
- Spartan UI (shadcn-style components for Angular): accessible primitives whose styled parts are copied into the repo and owned by us.
- Angular CDK for drag and drop and overlays.
- Fallback if Spartan proves immature for a need: Angular Material for that component only.

## Consequences
- Full control over the look; tokens from the design epic (E2) map straight to the Tailwind theme.
- More components to style ourselves than with a ready-made suite.
