# 0020 Merging ingredients: target wins, conversions only between equal base units, alias limit holds

**Status:** Accepted, 2026-11-09. Details the merge of [0019](0019-ingredient-catalog.md); uses [0015](0015-cross-module-transactions.md).

## Context
Agents will add near-duplicates to the catalog, so merging two ingredients is a routine clean-up, not a rare repair. Several questions were left open by 0019 and the PAN-29 prototype: what happens to unit conversions when the two ingredients are summed in different base units, what happens when the target would end up with more aliases than an ingredient may have, and how two merges of the same pair in opposite directions behave.

## Decision
- **Target wins.** The ingredient that stays keeps its name, base unit, category and staple flag. The source's name and aliases become aliases of the target, the source is deleted and the target is confirmed (*needs review* cleared).
- **Conversions are copied only between equal base units.** A conversion the target lacks is copied when both ingredients have the same base unit. With different base units none is copied, because *1 clove = 5 g* means nothing for an ingredient summed in pieces; the preview lists them as skipped and warns that quantities in the source's base unit will not be summed until a conversion is added. Nothing is converted automatically.
- **The alias limit holds.** A merge that would leave the target with more than 20 aliases is refused with 409; the preview says so (`exceedsAliasLimit`), and a person removes aliases first. The limit keeps the edit form usable and is cheap to hit only for ingredients that already collect many spellings.
- **One transaction, rows locked in id order.** `POST /api/catalog/ingredients/{target}/merge` begins the catalog transaction, locks both ingredient rows ordered by id, lets every `IIngredientReferenceHandler` move its references inside that transaction, deletes the source (freeing its names) and then saves the target with the new aliases. Any failure rolls everything back. Two opposite merges of the same pair run one after another; the second finds its source gone and answers 404.
- **Preview is read-only.** `GET /api/catalog/ingredients/{target}/merge-preview?sourceId=` returns both ingredients, the aliases to add, the alias count after the merge, whether the base units match, and the reference counts per module for the source and the target, so the SPA can show what moves and let the person swap which one stays.

## Consequences
- Merging never changes how existing target quantities are summed; a mismatched base unit is visible before the merge, not after.
- References keep their own quantities and units; a handler only re-points them and resolves clashes by its module's rules (for example dislikes: *never* wins over *avoid*).
- A failing module blocks the merge as a whole, which keeps references consistent at the cost of a merge being only as reliable as the least reliable handler.
