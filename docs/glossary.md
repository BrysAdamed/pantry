# Glossary

Use these terms in code, API, UI copy and tickets. Polish UI labels are in parentheses where useful.

| Term | Meaning |
|---|---|
| **Household** | The single group of people a deployment serves. Exactly one per deployment. |
| **Member** | A person in the household. Has a portion factor. May or may not have a user account (a toddler is a member, not a user). |
| **User** | Someone who can sign in. Belongs to the household. |
| **Admin** | A user who can also manage users: invite, issue recovery links, remove users, promote another admin. The first user is the admin; the last admin cannot be removed or demoted. |
| **First run** | The setup on a fresh deployment (*Pierwsze uruchomienie*): while no user exists, the app asks for a name and creates the admin with a passkey. |
| **Portion factor** | How much of a standard serving a member eats (e.g. adult 1.0, toddler 0.3). Configurable. |
| **Household serving** | Sum of portion factors of members eating a meal. Drives shopping quantities. |
| **Meal slot** | A configured meal of the day (e.g. breakfast, second breakfast, lunch, snack, dinner), with an order. |
| **Plan entry** | A recipe placed in a meal slot on a date. Can be *proposed*, *accepted* or *eaten*. |
| **Empty slot marker** | A plan entry with no recipe that asks an agent to fill it. |
| **Proposed** | A plan entry created by an agent and not yet accepted by a user. |
| **Leftover** | A plan entry that eats a dish cooked in an earlier entry. Adds nothing to the shopping list. |
| **Tray** | The planner side panel (*Szuflada dań*) with favourite, recent and searched recipes to place in the plan. A bottom sheet on phones. |
| **Recipe** | A dish with ingredient lines (quantity per one serving), steps, tags and optional nutrition. |
| **Ingredient** | A canonical catalog item (e.g. *onion*), with a base unit and optional conversions. |
| **Base unit** | The unit an ingredient is summed in: `g`, `ml` or `piece`. |
| **Unit conversion** | Ingredient-specific rule like *1 clove = 5 g*. Quantities without a conversion are not summed into the base unit. |
| **Staple** | An ingredient that is always at home and is left off generated lists by default. |
| **Dislike** | A household-wide ingredient preference: *never* (allergy, hard no) or *avoid*. |
| **Planning rule** | A free-text or structured rule agents follow when planning (e.g. weekday dinners under 30 minutes). |
| **Rating** | Household feedback on a recipe after it was eaten, plus a *make again* flag. |
| **Cooldown** | Minimum time before a favourite should be planned again. |
| **Shopping list** | A named list of items. Either *generated* from the plan for a date range or *general* (drugstore, pets...). |
| **List item** | A line on a shopping list: ingredient or free text, quantity, state (open, have at home, bought). |
| **Order** | A merged set of list items handed to an agent for a cart run. |
| **Cart run** | An agent filling the online shop cart from an order. Happens outside the app. |
| **Cart run report** | Per-item result of a cart run: *added*, *quantity changed*, *substituted*, *missing*. |
| **Product memory** | Which shop product was used for an ingredient last time. Shop-agnostic text and URL. |
| **Content language** | Household setting telling agents which language to write recipes and items in. |
| **UI language** | The language of labels in the app (*Język interfejsu*): `pl` or `en`, chosen per user. Independent of the content language. |
| **Theme** | Light or dark look of the app (*Motyw*). Follows the system unless the user picks one. |
| **Passkey** | The sign-in credential (*Klucz dostępu*) stored by the phone or computer. No passwords (ADR 0004). |
| **Access token** | Personal token an MCP client uses to call the app. Revocable. |
