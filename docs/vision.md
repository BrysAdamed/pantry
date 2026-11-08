# Vision

**Pantry** (Polish UI name: *Spiżarnia*) is a self-hosted meal planner and shared shopping list for one household. AI agents work with it through an MCP server.

## Why

The starting point was a weekly routine split across two chat assistants: one proposed a 7-day menu and a shopping list, the other filled an online shop cart from that list. It worked, but:

| Pain | Root cause |
|---|---|
| Every week brings new dishes, and the good ones never come back | Nothing remembers recipes, ratings or history |
| The agent has to be told what we don't eat, again and again | Preferences live in people's heads |
| A week of meals is hard to review in a chat | Wrong medium. It should be a calendar |
| Swapping a dish and recalculating the list is error-prone | List aggregation is done by an LLM instead of code |
| Non-food items (coffee, dog food, shampoo) get lost in messages | No shared list |
| Text is copied from one assistant to another | No integration point for agents |

## Who

A household of a few people (in our case two adults and a toddler) on Android and iPhone browsers, plus agents running in Claude Desktop on a home computer.

## What it is

- A **recipe library** that remembers what we cooked, what we liked, and what we never want again.
- A **calendar planner**: open the app, see the week, drag dishes in and out, leave empty slots for an agent to fill.
- **Deterministic shopping lists**, generated from the plan for any date range, plus shared general lists.
- An **MCP server** so an agent can read preferences and history, propose a plan, and report back what it put in the shop cart.
- An **offline-capable PWA**: the shopping list works in a shop with no signal.

## What it is not

- No native mobile apps. Responsive web and PWA only.
- No agents inside the app. Agentic flows run in Claude Desktop through MCP.
- No shop integration. The cart run is done by an agent with a browser; the app stores the result.
- No pantry inventory tracking.
- No multi-tenancy. One deployment serves one household.
- No public internet exposure required.

## Target experience

1. Sunday: ask Claude to plan next week. It reads the household profile, rules and history, fills the empty slots, and the proposals show up outlined in the calendar.
2. Review the week on the phone. Drag out what we don't fancy, drag in an old favourite from the tray, accept the rest.
3. Generate the shopping list for the week, tick what we already have, add general items.
4. Ask Claude to fill the cart. It reads the list over MCP, fills the cart, and reports each item: added, quantity changed, substituted, or missing.
5. Check out and pay manually. Missing items stay on the list for a trip to a physical shop, offline if needed.

## Idea log

Every idea from the kick-off session and what was decided. Epics are in the `PAN` project of the ticketing app.

| # | Idea | Decision | Epic |
|---|---|---|---|
| 1 | Recipe library (ingredients per serving, steps, tags, prep time, source) | Accepted | E5 |
| 2 | Ratings and "make again" | Accepted | E5 |
| 3 | Cooking history and favourite cooldown | Accepted | E5 |
| 4 | Import recipe from URL (done by the agent through MCP) | Accepted | E5, E8 |
| 5 | Recipe photos | Accepted (could) | E5 |
| 6 | Household dislikes: *never* / *avoid* | Accepted, household-wide | E3 |
| 7 | Planning rules as data | Accepted | E3 |
| 8 | Warning for *never* ingredients | Accepted | E5 |
| 9 | Week calendar with configurable slots and drag and drop | Accepted | E6 |
| 10 | Tray with favourites, recent dishes and search | Accepted | E6 |
| 11 | Empty slots for the agent | Accepted | E6 |
| 12 | Leftovers (cook once, eat twice) | Accepted | E6 |
| 13 | Servings override per slot | Accepted | E6 |
| 14 | Mobile day-swipe view | Accepted | E6 |
| 15 | Ingredient catalog and unit normalisation | Accepted | E4 |
| 16 | Generate list for a date range | Accepted | E7 |
| 17 | "Always at home" staples | Accepted | E4, E7 |
| 18 | Shared general lists with live sync | Accepted | E7 |
| 19 | Merged order for the agent | Accepted | E7 |
| 20 | Cart run report stored in the app | Accepted | E7, E8 |
| 21 | Ingredient to product memory | Accepted | E7 |
| 22 | Offline shopping list | Accepted, must | E9 |
| 23 | Recurring items | **Rejected** | |
| 24 | Pantry inventory | **Rejected** | |
| 25 | MCP server in the app | Accepted | E8 |
| 26 | Personal access tokens for MCP | Accepted | E8 |
| 27 | Agent skills and prompts in the repo | Accepted | E8 |
| 28 | Agent changes land as *proposed* | Accepted | E6, E8 |
| 29 | One household per deployment, Docker Compose | Accepted | E1, E10 |
| 30 | Passkeys | Accepted | E3 |
| 31 | Automated Postgres backups | Accepted | E10 |
| 32 | Seed data | **Changed**: production starts empty; an optional demo seed is allowed | E1 |
| 33 | PWA | Accepted | E9 |
| 34 | Push notifications | To discuss (iOS limits) | E9 |
| 35 | i18n PL and EN | Accepted | E1 |
| 36 | Design phase before UI work | Accepted, using Claude artifacts, not Figma | E2 |
| 37 | Cooking mode | Accepted (could) | E5 |
| 38 | Nutrition | **Changed**: stored when the source has it, summed per day and week | E5, E6 |
| 39 | Cost tracking from cart reports | Accepted (could) | E7 |
| 40 | Seasonal hints | **Rejected** | |
| 41 | Preferred content language for agents | Accepted (new) | E3, E8 |
| 42 | Portion factor per household member | Accepted (new) | E3 |
| 43 | Different dish per member in one slot | Postponed to V2 | |

## Epics

| Key | Epic |
|---|---|
| PAN-1 | E1 Foundation: scaffold, CI, docs, i18n |
| PAN-2 | E2 Design: visual direction, tokens, prototypes |
| PAN-3 | E3 Identity and household |
| PAN-4 | E4 Ingredient catalog and units |
| PAN-5 | E5 Recipes |
| PAN-6 | E6 Meal planner calendar |
| PAN-7 | E7 Shopping lists, sharing and cart run report |
| PAN-8 | E8 MCP server and agent skills |
| PAN-9 | E9 Offline-first PWA |
| PAN-10 | E10 Deployment and ops |
