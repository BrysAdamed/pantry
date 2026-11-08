# 0008 Shopping list computed in code

**Status:** Accepted, 2026-11-08

## Context
Asking an LLM to recalculate the list after swapping a dish was error-prone. Recipes use mixed units (pieces, spoons, cans, grams); shops sell packs.

## Decision
- Ingredients are canonical catalog items with a **base unit**: `g`, `ml` or `piece`.
- Ingredient-specific **conversions** map other units to the base unit (e.g. 1 clove = 5 g, 1 tbsp = 15 ml, 1 can = 400 g). Generic conversions (kg to g, l to ml) are built in.
- Recipe quantities are stored **per one serving**. Quantity needed = per-serving quantity × household serving (sum of portion factors, or the slot's override).
- Leftover entries contribute nothing. Staples are excluded by default.
- A quantity that cannot be converted stays a **separate line**; it is never summed with a guess.
- Regenerating a list keeps manual changes (ticks, extra items) where the item still exists.
- Rounding to pack sizes is not done by the app. The agent does it during the cart run and reports quantity changes.

## Consequences
- The list is reproducible and testable. Unit tests cover aggregation rules.
- The catalog needs upkeep (merging duplicates agents create); the UI and MCP must support merge.
