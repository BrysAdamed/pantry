# 0010 Languages

**Status:** Accepted, 2026-11-08

## Context
The repo is public. The household uses Polish; others may not. Recipe content comes from many sources in many languages.

## Decision
- Code, docs, tickets and commit messages: English.
- UI: Polish and English, chosen per user. No hardcoded UI strings.
- Product name: *Pantry*, shown as *Spiżarnia* in the Polish UI. Code uses `Pantry`.
- User content (recipes, ingredients, items) is stored as entered, in any language.
- The household has a **content language** setting. The MCP server tells agents to write and translate everything they add into it.

## Consequences
- Agents keep content consistent without being told each time.
- Ingredient aliases help when the same item arrives in two languages.
