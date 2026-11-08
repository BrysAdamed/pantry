# 0007 Offline: read everything, write shopping lists only

**Status:** Accepted, 2026-11-08

## Context
Phones only reach the app at home (see 0006), and shops often have poor signal. Full offline editing of every feature would need a sync engine and conflict resolution between two phones.

## Decision
- The PWA caches the app shell and the data needed to browse: plan, recipes, lists.
- Offline **writes** are allowed for shopping list items only: tick, untick, add, edit, remove. They queue in an IndexedDB outbox and replay on reconnect.
- Conflicts are resolved last-write-wins per list item, using a server-side version/timestamp.
- Other edits (planner, recipes, settings) require a connection; the UI disables them offline and says why.
- The UI always shows online, offline and pending-sync state.

## Consequences
- Covers the real use case (shopping) at a fraction of the complexity.
- List item APIs must be idempotent (client-generated IDs) so replays are safe.
- iOS may clear PWA storage after long disuse; the app must tolerate an empty cache.
