# 0022 Possible duplicates by trigram similarity of name keys; not duplicates stored per pair

**Status:** Accepted, 2026-11-09. Builds on [0019](0019-ingredient-catalog.md).

## Context
Agents and the recipe editor create ingredients from any name (PAN-31), so the catalog collects near-duplicates that the unique name key does not catch: *cebula* / *cebula żółta*, *pomidor* / *pomidory*. The ingredients screen (PAN-29) shows them as *possible duplicates* that a person merges or marks as *not duplicates*. The catalog of one household holds hundreds to a few thousand names, and the check has to work for Polish without a dictionary.

Options considered:
- **Levenshtein distance in code** over all name pairs: no extension, but quadratic in the application and poor for added words (*cebula* vs *cebula żółta* is 6 edits).
- **Full-text search**: built for documents and needs a language dictionary; it does not rate how alike two short names are.
- **`pg_trgm` similarity**: rates two strings by shared three-letter sequences, handles added words and endings, and a GIN index finds similar names without comparing every pair.

## Decision
- **`pg_trgm` over the comparison keys.** The Catalog migration enables the extension (`CREATE EXTENSION IF NOT EXISTS pg_trgm`, in the default schema) and adds a GIN trigram index on `catalog.ingredient_names.key`. The key already has no case or diacritics (0019), so similarity ignores them too, without `unaccent`.
- **A pair's score is the best similarity between any name or alias of one ingredient and any of the other.** Pairs at or above the threshold are listed most similar first. The query sets `pg_trgm.similarity_threshold` for its own transaction and uses the `%` operator, so the index is used.
- **Configurable threshold.** `Catalog:PossibleDuplicates:Threshold`, 0.1 to 1, default 0.4: *cebula* / *cebulka* (0.5) and *pomidor* / *pomidory* (0.7) are found, *ser żółty* / *ser biały* (0.25) is not.
- **Not duplicates per pair.** `catalog.not_duplicate_pairs` holds one row per dismissed pair, ids in uuid order (a check constraint keeps it so), with foreign keys that cascade: deleting an ingredient, or merging it away, removes its pairs. Marking a pair again does nothing.
- **Contract and REST.** `ICatalogModule.GetPossibleDuplicatesAsync` / `MarkNotDuplicatesAsync`, `GET /api/catalog/possible-duplicates` (optionally for one ingredient) and `POST /api/catalog/not-duplicates`. MCP tools (E8) wrap the contract.

## Consequences
- The database needs the `pg_trgm` extension, which ships with the official Postgres images. A database user that cannot create extensions needs it created once by an administrator.
- Trigram similarity also pairs different ingredients that share most letters (*mleko* / *mleko kokosowe*). A person dismisses such a pair once and it does not come back.
- A dismissal stays when the names of the two ingredients change later; only deleting or merging away one of them clears it.
