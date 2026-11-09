# 0020 ICU plural and select labels

**Status:** Accepted, 2026-11-09

## Context
Polish has three plural forms plus the genitive singular for fractions (1 ząbek, 2 ząbki, 5 ząbków, 2,5 ząbka), and the copy guide asks for ICU plural messages in the translation files. Transloco only interpolates `{{param}}`. Its MessageFormat plugin pulls in `@messageformat/core` (about 70 kB of the initial bundle, mostly plural rules for every locale) and compiles every label after the parameters are put in, so a `{` typed by a person into a search box or a name breaks the label.

## Decision
- A label that contains an ICU `plural`, `selectordinal` or `select` argument is an ICU message: `{count, plural, one {…} few {…} many {…} other {…}}`. Every other label keeps `{{param}}`.
- The app's own `IcuTranspiler` (`core/i18n/`) renders ICU labels. It supports the subset the app needs: `{arg}`, `plural`, `selectordinal`, `select`, exact `=N` cases and `#`. Plural categories come from the browser's `Intl.PluralRules` and `#` from `Intl.NumberFormat`, both with the active UI language, so no locale data ships with the app.
- Parameters are values of the parsed message, never part of the text that is parsed.
- A unit test parses every ICU label in both language files.

## Consequences
- Plural forms live in the translation files, next to the other labels; components pass a count and never build plurals by hand.
- Each language uses only the plural categories it has (`one`/`other` in English, `one`/`few`/`many`/`other` in Polish); `other` is required.
- Apostrophe quoting, `offset` and number/date arguments of full ICU are not supported. Format numbers and dates in code with Angular's locale functions and pass them in as text.
- Plural rules follow the active Transloco language; translating into a language that is not active uses the active language's rules.
