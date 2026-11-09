import { expect, Page, Route, test } from '@playwright/test';

interface Ingredient {
  id: string;
  name: string;
  aliases: string[];
  baseUnit: string;
  category: string;
  isStaple: boolean;
  needsReview: boolean;
  conversions: { unit: string; amount: number }[];
}

type Seed = Partial<Ingredient> & Pick<Ingredient, 'id' | 'name'>;

const sample: Seed[] = [
  { id: 'onion', name: 'onion', aliases: ['shallot'], conversions: [{ unit: 'piece', amount: 150 }] },
  { id: 'yellow-onion', name: 'yellow onion', needsReview: true },
  { id: 'garlic', name: 'garlic', conversions: [{ unit: 'clove', amount: 5 }] },
  { id: 'milk', name: 'milk', baseUnit: 'ml', category: 'dairyAndEggs' },
  { id: 'eggs-l', name: 'eggs L', baseUnit: 'piece', category: 'dairyAndEggs', needsReview: true },
  { id: 'salt', name: 'salt', category: 'spices', isStaple: true },
];

const key = (value: string) => value.trim().toLowerCase().normalize('NFD').replace(/\p{Mn}/gu, '').replace(/ł/g, 'l');

/** An in-memory catalog behind the `/api/catalog` routes, with reference counts per ingredient. */
async function mockCatalog(page: Page, seed: Seed[] = sample, references: Record<string, { module: string; count: number }[]> = {}) {
  const ingredients: Ingredient[] = seed.map((s) => ({
    aliases: [],
    baseUnit: 'g',
    category: 'vegetablesAndFruit',
    isStaple: false,
    needsReview: false,
    conversions: [],
    ...s,
  }));
  let next = 1;

  const conflictsOf = (change: Ingredient, id: string | null) =>
    [change.name, ...change.aliases].flatMap((name) =>
      ingredients
        .filter((i) => i.id !== id && [i.name, ...i.aliases].some((n) => key(n) === key(name)))
        .map((i) => ({ name, ingredientId: i.id, ingredientName: i.name })),
    );

  const list = (route: Route, url: URL) => {
    const search = key(url.searchParams.get('search') ?? '');
    const flag = (name: string) => url.searchParams.get(name);
    const items = ingredients
      .filter((i) => !search || [i.name, ...i.aliases].some((n) => key(n).includes(search)))
      .filter((i) => flag('needsReview') === null || String(i.needsReview) === flag('needsReview'))
      .filter((i) => flag('staple') === null || String(i.isStaple) === flag('staple'))
      .filter((i) => !flag('category') || i.category === flag('category'))
      .sort((a, b) => a.name.localeCompare(b.name));
    const page = Number(flag('page') ?? 1);
    const pageSize = Number(flag('pageSize') ?? 50);
    return route.fulfill({ json: { items: items.slice((page - 1) * pageSize, page * pageSize), total: items.length, page, pageSize } });
  };

  await page.route(/\/api\/catalog\/ingredients(\/.*)?(\?.*)?$/, async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const [id, action] = url.pathname.split('/').slice(4);
    const index = ingredients.findIndex((i) => i.id === id);
    if (!id) {
      if (request.method() === 'GET') {
        return list(route, url);
      }
      const change = { ...request.postDataJSON(), id: '', needsReview: false } as Ingredient;
      const conflicts = conflictsOf(change, null);
      if (conflicts.length) {
        return route.fulfill({ status: 409, json: { title: 'Another ingredient already has this name.', conflicts } });
      }
      change.id = `new-${next++}`;
      ingredients.push(change);
      return route.fulfill({ status: 201, json: change });
    }
    if (index < 0) {
      return route.fulfill({ status: 404 });
    }
    if (action === 'references') {
      return route.fulfill({ json: { references: references[id] ?? [] } });
    }
    if (action === 'confirm') {
      ingredients[index].needsReview = false;
      return route.fulfill({ json: ingredients[index] });
    }
    switch (request.method()) {
      case 'PUT': {
        const change = { ...ingredients[index], ...request.postDataJSON() } as Ingredient;
        const conflicts = conflictsOf(change, id);
        if (conflicts.length) {
          return route.fulfill({ status: 409, json: { conflicts } });
        }
        ingredients[index] = change;
        return route.fulfill({ json: change });
      }
      case 'DELETE':
        if (references[id]?.length) {
          return route.fulfill({ status: 409, json: { title: 'The ingredient is in use.', references: references[id] } });
        }
        ingredients.splice(index, 1);
        return route.fulfill({ status: 204 });
      default:
        return route.fulfill({ json: ingredients[index] });
    }
  });
  return ingredients;
}

test.beforeEach(async ({ page }) => {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    route.fulfill({ json: { id: '1', displayName: 'Alex', role: 'admin', userName: null, uiLanguage: 'en', theme: 'system', signInMethods: ['passkey'] } }),
  );
  await page.addInitScript(() => localStorage.setItem('pantry.uiLanguage', 'en'));
});

const list = (page: Page) => page.getByTestId('ingredient-list');
const row = (page: Page, name: string) => list(page).getByTestId('ingredient-row').filter({ has: page.getByTestId('ingredient-name').getByText(name, { exact: true }) });
const names = (page: Page) => list(page).getByTestId('ingredient-name');
const title = (page: Page) => page.getByTestId('ingredient-title');

/** Back to the list: the back link on phones; the list is always there from `md` up. */
async function backToList(page: Page) {
  const back = page.getByTestId('ingredient-back');
  if (await back.isVisible()) {
    await back.click();
  }
}

test('lists the catalog by category and finds ingredients by name, alias and filters', async ({ page }) => {
  await mockCatalog(page);
  await page.goto('/ingredients');

  await expect(page.getByRole('navigation', { name: 'Main navigation' }).getByRole('link', { name: /Ingredients/ })).toHaveAttribute(
    'aria-current',
    'page',
  );
  await expect(list(page).getByRole('heading', { level: 3 })).toHaveText([/Tidy up/, /Vegetables and fruit\s*3/, /Dairy and eggs\s*2/, /Spices\s*1/]);
  await expect(names(page)).toHaveText(['garlic', 'onion', 'yellow onion', 'eggs L', 'milk', 'salt']);
  await expect(page.getByTestId('ingredient-count')).toHaveText('6 ingredients');
  await expect(row(page, 'salt').getByTestId('badge-staple')).toBeVisible();

  await page.getByRole('searchbox', { name: 'Search ingredients' }).fill('SHAL');
  await expect(names(page)).toHaveText(['onion']);
  await expect(row(page, 'onion').getByTestId('alias-match')).toHaveText('matches: shallot');
  await expect(page.getByTestId('ingredient-count')).toHaveText('1 ingredient (of 6)');

  await page.getByRole('searchbox', { name: 'Search ingredients' }).fill('leek');
  await expect(page.getByTestId('no-results')).toContainText('Nothing matches "leek".');
  await expect(page.getByRole('link', { name: 'Add "leek"' })).toBeVisible();

  await page.getByRole('button', { name: 'Clear filters' }).click();
  await page.getByTestId('filter-needs-review').click();
  await expect(names(page)).toHaveText(['yellow onion', 'eggs L']);
  await page.getByTestId('filter-needs-review').click();
  await page.getByTestId('filter-staples').click();
  await expect(names(page)).toHaveText(['salt']);
  await page.getByTestId('filter-staples').click();
  await page.getByTestId('filter-category').selectOption({ label: 'Dairy and eggs' });
  await expect(names(page)).toHaveText(['eggs L', 'milk']);
});

test('edits an ingredient: name, alias, category, staple and conversions', async ({ page }) => {
  const catalog = await mockCatalog(page);
  await page.goto('/ingredients');

  await row(page, 'garlic').click();
  await expect(page).toHaveURL('/ingredients/garlic');
  await expect(title(page)).toHaveText('garlic');
  await expect(page.getByTestId('usage')).toContainText('Nowhere. You can delete it.');
  await expect(page.getByTestId('conversion')).toHaveText(['1 clove = 5 g']);
  await expect(page.getByTestId('conversions')).toContainText('Built in, always work: 1 kg = 1,000 g.');

  await page.getByTestId('ingredient-name-input').fill('Garlic');
  await page.getByRole('textbox', { name: 'New alias' }).fill('garlic bulb');
  await page.getByRole('textbox', { name: 'New alias' }).press('Enter');
  await page.getByRole('textbox', { name: 'New alias' }).fill('GARLIC');
  await page.getByRole('textbox', { name: 'New alias' }).press('Enter');
  await expect(page.getByTestId('alias-error')).toHaveText('This ingredient already has that name.');
  await page.getByRole('textbox', { name: 'New alias' }).fill('');
  await page.getByTestId('category-select').selectOption({ label: 'Spices' });
  await page.getByRole('switch', { name: 'Staple' }).click();
  await page.getByTestId('conversion-unit').selectOption({ label: 'pc' });
  await page.getByTestId('conversion-amount').fill('0');
  await page.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(page.getByTestId('conversion-error')).toHaveText('The amount must be greater than zero.');
  await page.getByTestId('conversion-amount').fill('40,5');
  await page.getByTestId('conversion-amount').press('Enter');
  await expect(page.getByTestId('conversion')).toHaveText(['1 clove = 5 g', '1 pc = 40.5 g']);
  await expect(page.getByRole('button', { name: 'Cancel' })).toBeVisible();

  await page.getByRole('button', { name: 'Save', exact: true }).click();

  await expect(page.getByTestId('toast')).toHaveText('Ingredient saved');
  await expect(title(page)).toHaveText('Garlic');
  await expect(page.getByRole('button', { name: 'Cancel' })).toHaveCount(0);
  expect(catalog.find((i) => i.id === 'garlic')).toMatchObject({
    name: 'Garlic',
    aliases: ['garlic bulb'],
    category: 'spices',
    isStaple: true,
    conversions: [
      { unit: 'clove', amount: 5 },
      { unit: 'piece', amount: 40.5 },
    ],
  });
  await backToList(page);
  await expect(row(page, 'Garlic')).toBeVisible();
});

test('asks before a new base unit removes the conversions', async ({ page }) => {
  const catalog = await mockCatalog(page);
  await page.goto('/ingredients/onion');

  await page.getByRole('radio', { name: /ml/ }).click();
  const dialog = page.getByRole('dialog', { name: 'Change the base unit to ml?' });
  await expect(dialog).toContainText('Conversions are stored in g and will be removed: 1 pc = 150 g.');
  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(page.getByRole('radio', { name: /^g/ })).toHaveAttribute('aria-checked', 'true');

  await page.getByRole('radio', { name: /ml/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Change unit' }).click();
  await expect(page.getByRole('radio', { name: /ml/ })).toHaveAttribute('aria-checked', 'true');
  await expect(page.getByTestId('conversion')).toHaveCount(0);
  await expect(page.getByTestId('conversions')).toContainText('1 l = 1,000 ml, 1 tbsp = 15 ml, 1 tsp = 5 ml');

  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByTestId('toast')).toHaveText('Ingredient saved');
  expect(catalog.find((i) => i.id === 'onion')).toMatchObject({ baseUnit: 'ml', conversions: [] });
});

test('walks the review queue from the Tidy up card and the nav badge', async ({ page }) => {
  await mockCatalog(page);
  await page.goto('/ingredients');

  await expect(page.getByTestId('needs-review-badge').filter({ visible: true })).toHaveText('2');
  const tidyUp = page.getByTestId('tidy-up');
  await expect(tidyUp).toContainText('2 ingredients to review');
  await tidyUp.getByRole('button', { name: 'Review' }).click();
  await expect(names(page)).toHaveText(['yellow onion', 'eggs L']);

  await row(page, 'yellow onion').click();
  const banner = page.getByTestId('review-banner');
  await expect(banner).toContainText('Needs review');
  await page.getByTestId('category-select').selectOption({ label: 'Other' });
  await banner.getByRole('button', { name: 'Confirm and next' }).click();

  await expect(page.getByTestId('toast')).toHaveText('Confirmed');
  await expect(page).toHaveURL('/ingredients/eggs-l');
  await expect(title(page)).toHaveText('eggs L');
  await expect(page.getByTestId('needs-review-badge').filter({ visible: true })).toHaveText('1');
  await expect(banner.getByRole('button', { name: 'Confirm and next' })).toHaveCount(0);
  await banner.getByRole('button', { name: 'Confirm', exact: true }).click();

  await expect(page.getByTestId('review-banner')).toHaveCount(0);
  await expect(page.getByTestId('needs-review-badge')).toHaveCount(0);
  await backToList(page);
  await expect(page.getByTestId('review-done')).toContainText('All reviewed');
  await expect(page.getByTestId('tidy-up')).toHaveCount(0);
});

test('adds an ingredient from a search with no match and explains a taken name', async ({ page }) => {
  const catalog = await mockCatalog(page);
  await page.goto('/ingredients');

  await page.getByRole('searchbox', { name: 'Search ingredients' }).fill('leek');
  await page.getByRole('link', { name: 'Add "leek"' }).click();
  await expect(title(page)).toHaveText('New ingredient');
  await expect(page.getByTestId('ingredient-name-input')).toHaveValue('leek');
  await expect(page.getByTestId('usage')).toHaveCount(0);

  await page.getByRole('textbox', { name: 'New alias' }).fill('Shallot');
  await page.getByRole('button', { name: 'Add ingredient' }).click();
  await expect(page.getByTestId('alias-error')).toHaveText('"Shallot" is already the name or an alias of "onion".');

  await page.getByRole('button', { name: 'Remove the alias Shallot' }).click();
  await page.getByRole('button', { name: 'Add ingredient' }).click();

  await expect(page.getByTestId('toast')).toHaveText('Ingredient added');
  await expect(page).toHaveURL(/\/ingredients\/new-1$/);
  await expect(title(page)).toHaveText('leek');
  expect(catalog.find((i) => i.name === 'leek')).toMatchObject({ aliases: [], category: 'other', baseUnit: 'g' });
});

test('deletes an unused ingredient and explains why a used one stays', async ({ page }) => {
  await mockCatalog(page, sample, { milk: [{ module: 'recipes', count: 3 }, { module: 'shopping', count: 1 }] });
  await page.goto('/ingredients/milk');

  await expect(page.getByTestId('usage')).toContainText(/Recipes\s*3/);
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  const inUse = page.getByRole('dialog', { name: 'Can\'t delete "milk"' });
  await expect(inUse.getByTestId('in-use')).toContainText('3 recipes, 1 shopping list.');
  await inUse.getByRole('button', { name: 'Close' }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);

  await backToList(page);
  await row(page, 'salt').click();
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  const confirm = page.getByRole('dialog', { name: 'Delete "salt"?' });
  await expect(confirm).toContainText('This cannot be undone.');
  await confirm.getByRole('button', { name: 'Delete' }).click();

  await expect(page.getByTestId('toast')).toHaveText('Ingredient deleted');
  await expect(page).toHaveURL('/ingredients');
  await expect(names(page)).not.toContainText(['salt']);
});

test('asks before leaving unsaved changes', async ({ page }) => {
  await mockCatalog(page);
  await page.goto('/ingredients/garlic');

  await page.getByTestId('ingredient-name-input').fill('garlic cloves');
  await page.getByRole('navigation', { name: 'Main navigation' }).filter({ visible: true }).getByRole('link', { name: 'Plan' }).click();
  const discard = page.getByRole('dialog', { name: 'Discard changes?' });
  await expect(discard).toContainText('Changes to "garlic" are not saved.');
  await discard.getByRole('button', { name: 'Keep editing' }).click();
  await expect(page).toHaveURL('/ingredients/garlic');
  await expect(page.getByTestId('ingredient-name-input')).toHaveValue('garlic cloves');

  await page.getByRole('navigation', { name: 'Main navigation' }).filter({ visible: true }).getByRole('link', { name: 'Plan' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Discard' }).click();
  await expect(page).toHaveURL('/planner');
});

test('welcomes an empty catalog', async ({ page }) => {
  await mockCatalog(page, []);
  await page.goto('/ingredients');

  await expect(page.getByRole('heading', { name: 'Ingredients show up on their own' }).filter({ visible: true })).toBeVisible();
  await page.getByRole('link', { name: 'Add an ingredient' }).filter({ visible: true }).click();
  await expect(page).toHaveURL('/ingredients/new');
  await expect(title(page)).toHaveText('New ingredient');
});
