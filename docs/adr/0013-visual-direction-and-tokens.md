# 0013 Visual direction and design tokens

**Status:** Accepted, 2026-11-09

## Context
E2 compared three visual directions (`docs/design/directions.html`) and variants of the favourite (`docs/design/direction-b-variants.html`). The app needs one set of tokens that Spartan components, our own components and later prototypes all use, in a light and a dark theme.

## Decision
- **Direction:** "Słoik konfitury" with the Konfitura palette: warm cream background, plum primary, apricot accent, 2px ink outlines with a hard offset shadow (3px on cards, 2px on controls), rounded corners (12px controls, 16px cards, 8px chips). Pressing a control moves it 1px and drops its shadow.
- **Fonts:** Bricolage Grotesque (headings, weight 800) and Figtree (body), both OFL, self-hosted from the Fontsource npm packages. The latin-ext subset covers Polish.
- **Tokens:** CSS custom properties in `src/Pantry.Web/src/styles.css`. Every colour is written once as `light-dark(light, dark)`. Spartan's semantic names (`--background`, `--primary`, `--accent`...) are filled from the palette, so copied Spartan components need no changes.
- **Agent colour:** everything an agent proposes (proposed plan entry, empty slot marker) uses the cool `agent` colour, never the accent.
- **Semantic state tokens:** glossary states get their own Tailwind colours (`entry-proposed`, `item-bought`, `report-missing`, `dislike-avoid`, `sync-pending`...) that point at a small set of base colours (`agent`, `success`, `warning`, `danger`, `info`). Components use the state token, so a state can be recoloured in one place.
- **Theme:** follows the system by default; a user can pick light or dark. The choice is stored per device in `localStorage` until E3 moves it to user settings, like the UI language (0011). The resolved theme is set as `data-theme` on `<html>`; Tailwind's `dark:` variant reads it.
- **Accessibility baseline:** text pairs meet WCAG AA (4.5:1) in both themes. The accent is only a fill behind ink text and always has an outline, never a text or border colour on its own. Controls are at least 44px tall on touch screens (`h-tap`). `prefers-reduced-motion` turns off transitions and animations.
- **Showcase:** `/design-system` renders the tokens, buttons and states with the real styles. It is not linked from the app.
- **Prototypes:** screen prototypes live in `docs/design/` as standalone HTML with generic data.

## Consequences
- One place to change a colour, and dark mode needs no duplicated selectors.
- `light-dark()` needs browsers from 2024 or newer (Safari 17.5, Chrome 123, Firefox 120). Fine for a household app.
- Before Angular starts, colours already follow the system theme, but a stored override and `dark:` utilities apply only after bootstrap, so a user who overrides the system theme may see a short flash.
- The prototype HTML files copy the palette values; after a token change they are not updated automatically.
