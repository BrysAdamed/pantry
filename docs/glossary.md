# Glossary

Use these terms in code, API, UI copy and tickets. Polish UI labels are in parentheses where useful.

| Term | Meaning |
|---|---|
| **Household** | The single group of people a deployment serves. Exactly one per deployment. |
| **Member** | A person in the household. Has a portion factor. May or may not have a user account (a toddler is a member, not a user). |
| **User** | Someone who can sign in. Belongs to the household. |
| **Admin** | A user who can also manage users: invite, issue recovery links, remove users, promote another admin. The first user is the admin; the last admin cannot be removed or demoted. |
| **Invite** | A link an admin issues for a member without a user (*Zaproszenie*). Valid 48 hours, single use. Opening it creates the user, links it to the member and gives them a passkey or a username and password. |
| **Recovery link** | A link an admin issues for an existing user who lost their device (*Link odzyskania*). Same rules as an invite; opening it adds a passkey or sets a new password, and signs the user out on their other devices. Invites and recovery links are both *passkey links* in code; a new one revokes the previous open one for the same member or user. |
| **First run** | The setup on a fresh deployment (*Pierwsze uruchomienie*): while no user exists, the app asks for a name and creates the admin with a passkey or a username and password. |
| **Portion factor** | How much of a standard serving a member eats (e.g. adult 1.0, toddler 0.3). Configurable. |
| **Household serving** | Sum of portion factors of members eating a meal. Drives shopping quantities. |
| **Meal slot** | A configured meal of the day (e.g. breakfast, second breakfast, lunch, snack, dinner), with an order. A row of the planner. Never deleted: a disabled slot leaves the planner but keeps its plan history; at least one stays enabled. |
| **Plan entry** | A recipe placed in a meal slot on a date. Can be *proposed*, *accepted* or *eaten*. |
| **Empty slot marker** | A plan entry with no recipe that asks an agent to fill it. |
| **Proposed** | A plan entry created by an agent and not yet accepted by a user. |
| **Leftover** | A plan entry that eats a dish cooked in an earlier entry. Adds nothing to the shopping list. |
| **Tray** | The planner side panel (*Szuflada dań*) with favourite, recent and searched recipes to place in the plan. A bottom sheet on phones. |
| **Recipe** | A dish with ingredient lines (quantity per one serving), steps, tags and optional nutrition. |
| **Ingredient** | A canonical catalog item (*Składnik*, e.g. *onion*): one name in the content language, aliases, a base unit, a category, a staple flag and optional unit conversions. Names and aliases are unique across the catalog regardless of letter case and diacritics. |
| **Catalog** | All ingredients of the household (*Katalog składników*). Other modules keep only ingredient ids. |
| **Alias** | Another name an ingredient is found by (*Alias*, e.g. *cebulka* for *cebula*). Used instead of translations: an ingredient has no per-language names. |
| **Category** | The group an ingredient belongs to on a shopping list (*Kategoria*): one of a fixed, built-in set in a fixed order (vegetables and fruit, dairy and eggs, meat and fish, bakery, dry and canned, spices, frozen, drinks, other). Default *other*. |
| **Unit** | A unit a quantity is written in (*Jednostka*), from a fixed vocabulary: `g`, `kg`, `ml`, `l`, `piece`, `clove`, `tbsp`, `tsp`, `pinch`, `can`, `jar`, `pack`, `slice`, `bunch`, `handful`, `glass`. |
| **Quantity** | An amount in a unit (*Ilość*, e.g. *300 g*). A total that cannot be added up into one unit has several parts, shown as *3 szt. + 300 g*. |
| **Base unit** | The unit an ingredient is summed in: `g`, `ml` or `piece`. |
| **Generic conversion** | A conversion that holds for every ingredient: `kg` to `g`, `l` to `ml`, `tbsp` = 15 ml, `tsp` = 5 ml. |
| **Unit conversion** | Ingredient-specific rule like *1 clove = 5 g*. Takes precedence over a generic conversion. One per unit; written in the base unit, so changing the base unit removes them. Quantities without a conversion are not summed into the base unit. |
| **Ingredient total** | What the quantity calculator gives for one ingredient: the sum in its base unit plus the amounts that could not be converted, summed per unit (*3 piece + 300 g*), with its category and staple flag. No rounding; pack sizes are the agent's job. An ingredient used only *to taste* has a total with no amount. |
| **Needs review** | Flag on an ingredient created automatically from an unknown name (*Do sprawdzenia*). Cleared when a person or an agent confirms it, or when another ingredient is merged into it. |
| **Merge** | Turning two ingredients into one (*Scal*): the source's names become aliases of the target, every module moves its references to the target, and the source is deleted. The target keeps its base unit, category and staple flag and is confirmed. The **merge preview** shows what will move before anything changes. |
| **Staple** | An ingredient that is always at home and is left off generated lists by default. |
| **Possible duplicate** | Two ingredients with similar names or aliases that may mean the same thing (*Możliwy duplikat*). People either merge them or mark them *not duplicates*, and a dismissed pair is not suggested again. |
| **Dislike** | A household-wide ingredient preference: *never* (allergy, hard no) or *avoid*. |
| **Planning rule** | A short free-text rule agents follow when planning (e.g. weekday dinners under 30 minutes), with an order. A disabled rule is kept for later but agents do not see it. Structured rules are out of scope for V1. |
| **Rating** | Household feedback on a recipe after it was eaten, plus a *make again* flag. |
| **Cooldown** | Minimum time before a favourite should be planned again. |
| **Shopping list** | A named list of items. Either *generated* from the plan for a date range or *general* (drugstore, pets...). |
| **List item** | A line on a shopping list: ingredient or free text, quantity, state (open, have at home, bought). |
| **Order** | A merged set of list items handed to an agent for a cart run. |
| **Cart run** | An agent filling the online shop cart from an order. Happens outside the app. |
| **Cart run report** | Per-item result of a cart run: *added*, *quantity changed*, *substituted*, *missing*. |
| **Product memory** | Which shop product was used for an ingredient last time. Shop-agnostic text and URL. |
| **Content language** | Household setting telling agents which language to write recipes and items in (*Język treści*). A BCP 47 code from a short supported list (`pl`, `en`, `de`, ...); the first run sets it from the admin's UI language. Changing it translates nothing that exists. |
| **UI language** | The language of labels in the app (*Język interfejsu*): `pl` or `en`, chosen per user. Independent of the content language. |
| **Theme** | Light or dark look of the app (*Motyw*). Follows the system unless the user picks one. |
| **Sign-in method** | A way a user signs in (*Sposób logowania*): *passkey* or *password*. A user has one or both (ADR 0018); the last one cannot be removed. |
| **Passkey** | The main sign-in credential (*Klucz dostępu*) stored by the phone or computer (ADR 0004). A user can have several and manages them in *My account*. |
| **Password** | The other sign-in method (*Hasło*): 12 to 128 characters, used with a *username*. Five wrong attempts lock the user out for 15 minutes. |
| **Username** | The login for password sign-in (*Nazwa użytkownika*): unique regardless of letter case, 3 to 32 letters, digits, `.`, `_` or `-`. Chosen once, together with the first password; a user who never set a password has none. Not the display name. |
| **My account** | The settings section (*Moje konto*) that applies only to the signed-in user: UI language, theme, passkeys, username and password, and signing out other devices. |
| **Session** | A signed-in browser (*Sesja*). Starts with a sign-in (passkey or password), lasts while the device keeps using the app (30 days idle by default) and ends with sign-out. |
| **Access token** | Personal token an MCP client uses to call the app. Revocable. |
