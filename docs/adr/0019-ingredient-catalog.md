# 0019 Ingredient catalog: fixed categories and units, aliases, review flag, merge through reference handlers

**Status:** Accepted, 2026-11-09. Builds on [0008](0008-deterministic-shopping-list.md), [0012](0012-module-persistence.md) and [0015](0015-cross-module-transactions.md).

## Context
Shopping lists are summed in code (0008), so every recipe line, dislike and list item must point at one canonical ingredient with a known base unit. Most ingredients will be typed by agents through MCP, in whatever words a recipe source uses, so the catalog will collect near-duplicates (*cebula*, *Cebula*, *cebulka*). The app has one household and one content language (0010); nobody needs the same ingredient in two languages. Categories only group a shopping list, and a household does not want to manage them.

## Decision
- **Catalog module.** `Modules/Catalog/`, schema `catalog`. Other modules keep only ingredient ids and read ingredients through `ICatalogModule`; MCP tools (E8) wrap the same contract.
- **Fixed categories and units in code.** `IngredientCategory` and `Unit` are enums with a fixed order. Their camelCase name is the code used by the API, the database (stored as text) and the SPA labels (`catalog.category.<code>`, `catalog.unit.<code>` in both language files; a test checks every code has a label). No management UI; a new category or unit is a code change. Numbers are never accepted for them. `GET /api/catalog/vocabulary` lists both with the base units and the generic conversions.
- **Base units and generic conversions.** An ingredient is summed in `g`, `ml` or `piece`. `kg`, `l`, `tbsp` (15 ml) and `tsp` (5 ml) convert for every ingredient; other units (clove, can, slice...) need an ingredient conversion (PAN-32), which takes precedence over the generic one.
- **Aliases instead of translations.** An ingredient has one name in the content language and any number of aliases (max 20). All names and aliases live in `catalog.ingredient_names` with a comparison key (lower case, no diacritics, whitespace collapsed; `ł` maps to `l`) under one unique index, so no two ingredients share a name or an alias and the database settles concurrent creates. Search matches part of any key.
- **Auto-created ingredients need review.** An unknown name from the recipe editor or an agent creates the ingredient instead of failing, with a guessed base unit, category *other* and *needs review* set (PAN-31). A person or an agent confirms it; a merge into it confirms it too. Ingredients a person creates on the ingredients screen do not need review.
- **Merge through reference handlers.** Each module that stores ingredient ids registers an `IIngredientReferenceHandler`: count references (used by delete and by the merge preview) and move references from the source to the target inside the catalog's transaction (0015), resolving clashes by its own rules. A merge moves the source's names to the target's aliases, deletes the source and commits only when every handler succeeded (PAN-34).

## Consequences
- Categories, units and their labels are versioned with the code, and the SPA, the API and agents use the same codes.
- One name per ingredient keeps lists readable; aliases cover other spellings and languages an agent might use.
- The catalog fills itself; the *needs review* filter and merge keep it clean without blocking anyone who adds a recipe.
- The catalog never reads other modules' tables. A module that starts referencing ingredients must register a handler, or merge and delete would leave its references dangling.
- Sorting by the comparison key keeps a predictable order independent of the database collation, at the price of not following a language's alphabet exactly (*żurek* sorts among the words with *z*, not after them).
