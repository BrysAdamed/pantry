# 0023 Recipe model: quantities per serving next to the entered ones, sections on the lines, archive instead of delete

**Status:** Accepted, 2026-11-09. Builds on [0008](0008-deterministic-shopping-list.md), [0012](0012-module-persistence.md) and [0019](0019-ingredient-catalog.md).

## Context
The planner and the shopping list (E6, E7) scale a recipe to the household serving of each plan entry and sum the result per ingredient (0008), so they need quantities for one serving. People and agents enter a recipe the way its source writes it: for 4 servings, with groups like *Sos* and *Ciasto*, with *salt to taste*. The editor has to show those numbers again, and `500 g / 3` per serving does not multiply back to `500 g`. Plan entries and history keep pointing at recipes for years, including the ones nobody wants any more.

## Decision
- **Recipes module.** `Modules/Recipes/`, schema `recipes`. Other modules read recipes through `IRecipesModule` (summaries and ingredient lines per serving) and keep only recipe ids. A recipe's tags, steps and ingredient lines are owned rows of the recipe (`recipe_tags`, `recipe_steps`, `recipe_ingredient_lines`), replaced as a whole by `PUT /api/recipes/{id}`.
- **Both quantities are stored.** A line keeps the quantity as entered for the *recipe servings* (`quantity`, 3 decimal places) and the same amount for one serving (`quantity_per_serving`, entered / servings rounded to 6 decimal places), recomputed on every save. Reads return both; other modules only see the per serving one. Nutrition is entered per serving and rounded to one decimal place.
- **Lines reference catalog ingredients by id.** A line sent with an ingredient name is resolved through `ICatalogModule.ResolveIngredientAsync` (an unknown name creates an ingredient that needs review), only after the whole request is valid, because the catalog commits on its own. A quantity always comes with a unit from the catalog vocabulary; a line with neither is *to taste*.
- **Sections are a name on the line.** Lines outside any section come first, then each section's lines together, so the order of the lines gives the order of the sections and no separate table is needed. A section without lines is not stored.
- **Archive, never delete.** `POST /api/recipes/{id}/archive` and `/restore` set and clear `archived_at`. The library and tag suggestions leave archived recipes out; reads by id (REST and the contract) still return them.

## Consequences
- The planner and the shopping list multiply per serving quantities by a household serving and never need the recipe servings; the editor shows exactly what was typed.
- A 6-place per serving value may be off by a millionth after multiplying back; shopping totals are not rounded anyway (0019) and the agent decides pack sizes.
- A request that fails after its ingredient names were resolved would leave new ingredients behind; validating everything first keeps that to failures of the save itself.
- Ingredient ids in lines have no foreign key into `catalog` (0012). The recipe ingredient reference handler (0019, PAN-52) counts lines, archived recipes included, so an ingredient used in any recipe cannot be deleted. A merge points the source's lines at the target and changes nothing else on them: a recipe with lines for both keeps two lines (quantities, units and notes as entered, which may differ), and `updated_at` stays, because nobody edited the recipe.
- Renaming a section or reordering lines rewrites the lines, which is cheap at recipe sizes (at most 100 lines).
