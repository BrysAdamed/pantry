# Copy guide

How the app talks, in Polish and English. Labels live in `src/Pantry.Web/public/i18n/{pl,en}.json` (ADR 0011). Terms come from `docs/glossary.md`.

## Voice

- **Warm and homely, never cute.** Like a note on the fridge from someone who lives with you. No jokes in errors, no exclamation marks except a real success ("Witaj w domu!").
- **Short.** A label is 1 to 3 words, a hint is one sentence, an empty state is a heading plus at most two sentences.
- **Say what happens, then what to do.** "Jesteś offline. Ustawienia zmienisz po powrocie do sieci."
- **The agent is "agent", not "AI" or "sztuczna inteligencja".** It works in Claude over MCP, so copy never suggests it runs inside the app. When an agent can do a job, give an example prompt.
- **No emoji** in UI copy. Icons do that job.

## Polish

- **Informal *ty*,** second person singular: "Zaplanuj tydzień", "Dodaj przepis", "Masz 3 propozycje".
- **Gender-neutral.** Polish past tense forms carry gender (*zrobiłeś / zrobiłaś*), and the app does not know the user's. Use the present tense, an impersonal form or a noun instead.

  | Avoid | Use |
  |---|---|
  | Zapisałeś przepis | Zapisano przepis / Przepis zapisany |
  | Skopiowałem, zamknij | Gotowe, zamknij |
  | Chciałeś 1,4 l | Na liście 1,4 l |
  | Listy, które otwierałeś | Listy otwarte wcześniej |
  | Czy jesteś pewien? | Na pewno? / Usunąć przepis? |

- **Plurals have three forms** (1 propozycja, 2 propozycje, 5 propozycji; 22 propozycje, 25 propozycji). Never hardcode "(y)" or build plurals by hand in templates; use ICU plural messages in the translation files.
- **Numbers and units:** decimal comma (2,3 porcji), space before units (200 g, 1,5 l), no space in "°C". Fractional amounts take the genitive singular: "2,5 ząbka", "0,5 łyżki". From 1000 g or ml up, write kg or l ("1,5 kg"); amounts in units that do not add up are joined with a plus ("3 szt. + 300 g"). The `quantity` pipe does all of this.
- **Dates:** day and short month in lowercase, "9 lis", "pn 9"; weekdays in lowercase. Use Angular's locale pipes, never hand-formatted strings.
- **Buttons are verbs in the imperative:** Dodaj, Zapisz, Akceptuj, Odrzuć, Unieważnij. Not "Dodawanie", not "OK".
- **Quotes:** Polish quotation marks „…”.

## English

- **Sentence case** everywhere, including headings and buttons: "Generate a list", not "Generate A List".
- **Plain second person:** "Plan this week", "You have 3 proposals".
- **Numbers:** decimal point (2.3 servings), space before units (200 g).
- **Quotes:** "…".

## Glossary labels

Use these exact words. If the meaning changes, change the glossary first.

| Glossary term | PL label | EN label |
|---|---|---|
| Plan entry | Pozycja planu | Plan entry |
| Proposed | Propozycja | Proposed |
| Accepted | Zaakceptowane | Accepted |
| Eaten | Zjedzone | Eaten |
| Leftover | Resztki | Leftover |
| Empty slot marker | Czeka na agenta | Waiting for the agent |
| Meal slot | Posiłek | Meal |
| Household serving | Porcja domowa | Household serving |
| Portion factor | Mnożnik porcji | Portion factor |
| Tray | Szuflada dań | Dish tray |
| Recipe | Przepis | Recipe |
| Staple | Podstawowe (zwykle w domu) | Staple |
| Dislike: never / avoid | Nigdy / Unikamy | Never / Avoid |
| Planning rule | Zasada planowania | Planning rule |
| Rating · make again | Ocena · Ugotować znowu? | Rating · Make again? |
| Shopping list (generated / general) | Lista z planu / Lista ogólna | List from the plan / General list |
| List item: open / have at home / bought | Do kupienia / Mam w domu / Kupione | To buy / Have at home / Bought |
| Order | Zamówienie | Order |
| Cart run report | Raport z koszyka | Cart run report |
| Report: added / quantity changed / substituted / missing | Dodano / Zmieniona ilość / Zamiennik / Brak | Added / Quantity changed / Substituted / Missing |
| Sync: online / offline / pending | Zapisane / Offline / Czeka na wysłanie | Saved / Offline / Waiting to sync |
| Content language | Język treści | Content language |
| UI language | Język interfejsu | Interface language |
| Access token | Token dostępu | Access token |
| Passkey | Klucz dostępu | Passkey |
| Admin | Administrator | Admin |
| Invite | Zaproszenie | Invite |
| Recovery link | Link odzyskania | Recovery link |
| Theme | Motyw | Theme |
| My account | Moje konto | My account |
| Ingredient | Składnik | Ingredient |
| Base unit | Jednostka bazowa | Base unit |
| Unit conversion | Przelicznik | Conversion |
| Alias | Alias | Alias |
| Category | Kategoria | Category |
| Needs review | Do sprawdzenia | Needs review |
| Possible duplicate | Możliwy duplikat | Possible duplicate |
| Merge | Połącz | Merge |

## Patterns

**Empty states** (see `empty-states.html`): heading names the goal, one sentence on why it is empty, one primary action. Add the agent box with an example prompt when the agent can help.

| Bad | Good |
|---|---|
| Brak danych | Zaplanuj ten tydzień |
| No recipes found. | Add your first recipe |

**Errors:** what happened, in the user's words, then what to do. No codes in the headline, no blame.

| Bad | Good |
|---|---|
| Błąd 401: Unauthorized | Sesja wygasła. Zaloguj się ponownie. |
| WebAuthn NotAllowedError | Logowanie przerwane. Spróbuj jeszcze raz. |
| Network error | You are offline. Shopping lists still work. |

**Confirmations** only for things that cannot be undone. The button repeats the verb: "Unieważnić token?" → [Anuluj] [Unieważnij]. Everything else is undoable and gets a short toast instead ("Zapisano przepis").

**Offline:** say what is blocked and what still works. "Ustawienia zmienisz po powrocie do sieci. Listy zakupów działają dalej."

**Agent copy:** proposals are offers, not facts. "Agent proponuje", "4 propozycje czekają na Twoją decyzję". Never "Agent zaplanował".

## Ingredients screen

Labels for the catalog (`ingredients.html`, PAN-29). Category and unit labels come with the catalog model (PAN-30); the prototype uses the ones below. `{n}` and `{name}` are ICU placeholders; counts use ICU plurals.

| Where | PL | EN |
|---|---|---|
| Navigation | Składniki | Ingredients |
| Tab badge (screen reader) | {n} do sprawdzenia | {n} need review |
| New button | Nowy | New |
| Search placeholder | Szukaj w nazwach i aliasach | Search names and aliases |
| Filter: needs review | Do sprawdzenia | Needs review |
| Filter: staples | Podstawowe | Staples |
| Filter: all categories | Wszystkie kategorie | All categories |
| Result count | {n} składnik / składniki / składników (z {total}) | {n} ingredient(s) (of {total}) |
| Clear filters | Wyczyść / Wyczyść filtry | Clear / Clear filters |
| Tidy-up card | Porządki | Tidy up |
| Tidy-up: review | {n} składnik / składniki / składników do sprawdzenia | {n} ingredient(s) to review |
| Tidy-up: duplicates | {n} możliwy duplikat / możliwe duplikaty / możliwych duplikatów | {n} possible duplicate(s) |
| Tidy-up button | Przejrzyj / Pokaż wszystkie | Review / Show all |
| Row: alias match | pasuje: {alias} | matches: {alias} |
| Row badges | do sprawdzenia · możliwy duplikat · podstawowy | needs review · possible duplicate · staple |
| Base unit tooltip | Jednostka bazowa | Base unit |
| No results | Nic nie pasuje do „{q}”. | Nothing matches "{q}". |
| No results action | Dodaj „{q}” | Add "{q}" |
| Review queue empty | Wszystko sprawdzone · Nowe składniki od agenta i z przepisów pojawią się tutaj. | All reviewed · New ingredients from the agent and recipes show up here. |
| Empty catalog title | Składniki pojawią się same | Ingredients show up on their own |
| Empty catalog text | Każdy składnik z nowego przepisu trafia tutaj. Agent też je dodaje, a Ty potem je sprawdzasz. | Every ingredient from a new recipe lands here. The agent adds them too, and you review them later. |
| Empty catalog action | Dodaj składnik | Add an ingredient |
| Empty catalog agent hint | Poproś Claude: „Dodaj do Spiżarni przepis na leczo z cukinią.” | Ask Claude: "Add a courgette stew recipe to Pantry." |
| Nothing selected | Wybierz składnik z listy. | Pick an ingredient from the list. |
| Detail title (new) | Nowy składnik | New ingredient |
| Merge button | Połącz z… | Merge with… |
| Review banner title | Do sprawdzenia | Needs review |
| Review banner text (agent) | Dodany automatycznie przez agenta {date} z przepisu „{recipe}”. Sprawdź kategorię i jednostkę bazową. | Added automatically by the agent on {date} from the recipe "{recipe}". Check the category and base unit. |
| Review banner text (recipe editor) | Dodany automatycznie w edytorze przepisu {date} … | Added automatically in the recipe editor on {date} … |
| Review banner text (until the catalog records who added it) | Dodany automatycznie, przez agenta albo z przepisu. Sprawdź kategorię i jednostkę bazową. | Added automatically, by the agent or from a recipe. Check the category and base unit. |
| Confirm | Zatwierdź / Zapisz i zatwierdź | Confirm / Save and confirm |
| Confirm and next | Zatwierdź i dalej | Confirm and next |
| Duplicate banner title | Możliwy duplikat: „{name}” | Possible duplicate: "{name}" |
| Duplicate banner text | Podobne nazwy. Jeśli to ten sam składnik, połącz je, a przepisy i listy przejdą na jeden. | Similar names. If they are the same ingredient, merge them and recipes and lists move to one. |
| Merge (pair) | Połącz… | Merge… |
| Not duplicates | To nie duplikaty | Not duplicates |
| Field: name | Nazwa · np. cebula | Name · e.g. onion |
| Name taken | Taka nazwa albo alias już jest: „{name}”. · Połącz z nim | This name or alias already exists: "{name}". · Merge with it |
| Field: aliases | Aliasy · Dodaj i Enter | Aliases · Type and press Enter |
| Aliases hint | Inne nazwy tego samego składnika. Wyszukiwarka i agent je rozpoznają. | Other names for the same ingredient. Search and the agent recognise them. |
| Alias taken | „{alias}” to już nazwa / alias składnika „{name}”. | "{alias}" is already the name / an alias of "{name}". |
| Alias duplicate | Ten składnik już ma taką nazwę. | This ingredient already has that name. |
| Field: category | Kategoria | Category |
| Category hint | Według kategorii układa się lista zakupów. | Shopping lists are grouped by category. |
| Field: base unit | Jednostka bazowa | Base unit |
| Base unit options | g · waga / ml · objętość / szt. · sztuki | g · weight / ml · volume / pc · pieces |
| Base unit hint | W tej jednostce sumują się ilości na liście zakupów. | Shopping list amounts are added up in this unit. |
| Staple toggle | Podstawowy · Zwykle w domu, jak sól czy olej. Nie trafia na listy z planu. | Staple · Usually at home, like salt or oil. Left off lists from the plan. |
| Conversions card | Przeliczniki | Conversions |
| Conversion row | 1 {unit} = {amount} {base} | 1 {unit} = {amount} {base} |
| No conversions | Bez przelicznika ilości w innych jednostkach nie dodadzą się do {base}, tylko trafią na listę osobno. | Without a conversion, amounts in other units are not added to {base}; they go on the list separately. |
| Add conversion | jednostka · Dodaj | unit · Add |
| Conversion errors | Wybierz jednostkę. · Ilość musi być większa od zera. | Pick a unit. · The amount must be greater than zero. |
| Built-in conversions | Wbudowane, zawsze działają: {list}. · Sztuki nie mają wbudowanych przeliczników. | Built in, always work: {list}. · Pieces have no built-in conversions. |
| Remove conversion (screen reader) | Usuń przelicznik {unit} | Remove the {unit} conversion |
| Usage card | Gdzie używany | Where it is used |
| Usage rows | Przepisy · Listy zakupów · Nie jemy | Recipes · Shopping lists · We don't eat |
| Unused | Nigdzie. Możesz go usunąć. | Nowhere. You can delete it. |
| Form bar | Niezapisane zmiany · Usuń · Anuluj · Zapisz · Dodaj składnik | Unsaved changes · Delete · Cancel · Save · Add ingredient |
| Base unit change | Zmienić jednostkę na {unit}? · Przeliczniki są zapisane w {base} i zostaną usunięte: {list}. · Zmiana zapisze się razem z resztą formularza. · [Anuluj] [Zmień jednostkę] | Change the base unit to {unit}? · Conversions are stored in {base} and will be removed: {list}. · The change is saved with the rest of the form. · [Cancel] [Change unit] |
| Delete unused | Usunąć „{name}”? · Nigdzie go nie używasz. Tego nie da się cofnąć. · [Anuluj] [Usuń] | Delete "{name}"? · It is not used anywhere. This cannot be undone. · [Cancel] [Delete] |
| Delete used | Nie można usunąć „{name}” · Jest używany · {usage}. · Jeśli to duplikat innego składnika, połącz je. Odwołania przejdą na ten, który zostaje. · [Zamknij] [Połącz z…] | Can't delete "{name}" · It is in use · {usage}. · If it duplicates another ingredient, merge them. References move to the one you keep. · [Close] [Merge with…] |
| Leaving unsaved changes | Odrzucić zmiany? · Zmiany w „{name}” nie są zapisane. / Nowy składnik nie jest zapisany. · [Wróć do edycji] [Odrzuć] | Discard changes? · Changes to "{name}" are not saved. / The new ingredient is not saved. · [Keep editing] [Discard] |
| Duplicates view | Możliwe duplikaty · Pary o podobnych nazwach. Połącz je albo oznacz, że to różne składniki, a para już nie wróci. | Possible duplicates · Pairs with similar names. Merge them or mark them as different, and the pair will not come back. |
| No duplicates | Brak duplikatów · Katalog jest uporządkowany. | No duplicates · The catalog is tidy. |
| Merge step 1 | Połącz składniki · 1 z 2 · Z czym połączyć „{name}”? · Wybierz składnik, który oznacza to samo. W następnym kroku zdecydujesz, który zostaje. | Merge ingredients · 1 of 2 · Merge "{name}" with what? · Pick the ingredient that means the same. Next you choose which one stays. |
| Merge step 2 | Który zostaje? | Which one stays? |
| Merge preview label | Co się stanie | What happens |
| Merge preview: removed | {gone} znika. Jego nazwy zostają aliasami „{keep}”: {list}. | {gone} goes away. Its names become aliases of "{keep}": {list}. |
| Merge preview: references | Przechodzi na „{keep}”: {usage}. | Moves to "{keep}": {usage}. |
| Merge preview: dislikes | „Nie jemy ({level})” przechodzi na „{keep}”. · Oba są w „Nie jemy”. Zostaje surowsze: {level}. | "We don't eat ({level})" moves to "{keep}". · Both are in "We don't eat". The stricter one stays: {level}. |
| Merge preview: conversions | Dochodzą przeliczniki: {list}. · Pominięte przeliczniki: {list} („{keep}” ma już swój / inna jednostka bazowa). | Conversions added: {list}. · Conversions skipped: {list} ("{keep}" has its own / different base unit). |
| Merge preview: kept fields | Kategoria, jednostka bazowa i „podstawowy” zostają z „{keep}”, a składnik zostaje zatwierdzony. | Category, base unit and staple stay from "{keep}", and it is marked as reviewed. |
| Merge unit warning | Różne jednostki bazowe ({a} i {b}). Ilości w {b} nie dodadzą się do {a}, dopóki nie dodasz przelicznika. | Different base units ({a} and {b}). Amounts in {b} are not added to {a} until you add a conversion. |
| Merge confirm | Tego nie da się cofnąć. · [Wstecz] [Anuluj] [Połącz] | This cannot be undone. · [Back] [Cancel] [Merge] |
| Toasts | Dodano składnik · Zapisano składnik · Zatwierdzono · Usunięto składnik · Połączono. Zostaje „{name}” · Para nie wróci jako duplikat | Ingredient added · Ingredient saved · Confirmed · Ingredient deleted · Merged. "{name}" stays · This pair won't come back as a duplicate |

**Ingredient picker** (shared component, PAN-39):

| Where | PL | EN |
|---|---|---|
| Placeholder | Szukaj składnika (host może podać przykład, np. „np. cebula”) | Search ingredients (hosts may pass an example, e.g. "e.g. onion") |
| Alias match | pasuje: {alias} | matches: {alias} |
| Already chosen | już na liście | already on the list |
| New option | Dodaj „{q}” · Nowy składnik, trafi do katalogu po zapisaniu | Add "{q}" · New ingredient, added to the catalog when you save |
| New badge / hint | nowy · Nowy składnik, trafi do katalogu jako „do sprawdzenia” | new · New ingredient, added to the catalog as "needs review" |
| No match (new not allowed) | Nie ma takiego składnika w katalogu. | No such ingredient in the catalog. |
| Search failed | Wyszukiwanie teraz nie działa. Spróbuj za chwilę. | Search is not working right now. Try again in a moment. |

## App name

The UI uses "Spiżarnia" in Polish and "Pantry" in English (`app.name`). The PWA manifest has one name, "Pantry", because one build serves both languages; the home screen shows "Pantry" for everyone.
