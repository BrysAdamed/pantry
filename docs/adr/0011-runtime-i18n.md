# 0011 Runtime UI translations with Transloco

**Status:** Accepted, 2026-11-09

## Context
The UI language is chosen per user (see 0010) and the app is one container serving one SPA build (see 0002). Angular's built-in i18n compiles one bundle per locale and switches by URL, which means a reload and a separate build per language.

## Decision
- Translations are loaded at runtime with Transloco from `public/i18n/<lang>.json` (`pl`, `en`).
- Templates use the `*transloco` directive. No UI string is written in a template or component.
- English is the default and fallback language. A missing key falls back to English and is logged in development.
- Until users exist (E3), the UI language is stored per device in `localStorage`, with the browser language as the first guess. E3 moves it to the user's settings.
- A unit test checks that every language file has the same keys and no empty labels.

## Consequences
- One build, language switch without a reload, `<html lang>` and the page title follow the language.
- Keys are strings, so typos are only caught by the missing-key log and tests, not by the compiler.
- Dates and numbers use Angular's locale pipes with the active language; locale data must be registered for both languages when the first such pipe appears.
