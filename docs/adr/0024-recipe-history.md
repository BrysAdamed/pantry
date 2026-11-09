# 0024 Recipe history lives in Recipes and is written by the Planner through the contract

**Status:** Accepted, 2026-11-09. Builds on [0002](0002-modular-monolith.md), [0015](0015-cross-module-transactions.md) and [0023](0023-recipe-model.md).

## Context
Ratings, *make again*, the cooldown, sorting by last eaten and agents all need to know when the household ate each dish. That fact starts in the Planner (E6): a plan entry becomes eaten after its day or by hand, and may be un-eaten again. Two places could own it:
- **Planner.** History is a view of eaten plan entries. Recipes would then read the Planner to rate, sort or compute a cooldown, and the Planner already reads Recipes for names and ingredients, so the two modules would depend on each other.
- **Recipes.** The Planner tells Recipes when an entry becomes eaten or un-eaten. Recipes owns everything that is said about a dish over time, and the dependency stays one way.

## Decision
- **History entries live in Recipes** (`recipes.history_entries`): recipe, date, meal slot id and plan entry id. Meal slot and plan entry ids are kept without foreign keys (0012); the recipe id has one, which is safe because recipes are never deleted (0023).
- **The Planner writes them through `IRecipesModule`**, inside its own transaction (0015), so a plan entry and its history entry change together: `RecordEatenAsync(EatenPlanEntry, transaction)` and `RecordUneatenAsync(planEntryId, transaction)`.
- **Idempotent per plan entry.** At most one history entry per plan entry (unique index). Recording it again keeps that entry and takes the recipe, date and meal slot it is given; un-eaten deletes it together with what hangs on it (its rating, PAN-43). A leftover is not history: recording a leftover removes the plan entry's history entry if it had one, so the Planner does not have to remember what it recorded before.
- **Reads** go through the contract (`GetLastEatenAsync` for many recipes, `GetHistoryAsync` newest first) and `GET /api/recipes/{id}/history`.

## Consequences
- Recipes does not depend on the Planner; the Planner depends on Recipes, as it already does for recipes and ingredients.
- The Planner must call the contract for every change of the eaten state, including deleting an eaten plan entry; a missed call leaves history out of step, and nothing reconciles it.
- History survives plan entries being cleaned up later, and it is there before the Planner exists, so ratings and cooldown can be built and tested now.
