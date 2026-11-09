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
- **Numbers and units:** decimal comma (2,3 porcji), space before units (200 g, 1,5 l), no space in "°C". Fractional amounts take the genitive singular: "2,5 ząbka", "0,5 łyżki".
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

## App name

The UI uses "Spiżarnia" in Polish and "Pantry" in English (`app.name`). The PWA manifest has one name, "Pantry", because one build serves both languages; the home screen shows "Pantry" for everyone.
