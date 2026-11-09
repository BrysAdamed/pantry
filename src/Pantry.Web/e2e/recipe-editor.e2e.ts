import { expect, Locator, Page, test } from '@playwright/test';

const catalog = [
  {
    id: 'onion',
    name: 'onion',
    aliases: [],
    baseUnit: 'g',
    category: 'vegetablesAndFruit',
    isStaple: false,
    needsReview: false,
    conversions: [],
  },
  {
    id: 'eggs',
    name: 'eggs',
    aliases: [],
    baseUnit: 'piece',
    category: 'dairyAndEggs',
    isStaple: false,
    needsReview: false,
    conversions: [],
  },
  {
    id: 'peanuts',
    name: 'peanuts',
    aliases: [],
    baseUnit: 'g',
    category: 'dryAndCanned',
    isStaple: false,
    needsReview: false,
    conversions: [],
  },
  {
    id: 'coriander',
    name: 'coriander',
    aliases: [],
    baseUnit: 'g',
    category: 'spices',
    isStaple: false,
    needsReview: false,
    conversions: [],
  },
];

const dislikes = [
  {
    id: 'd1',
    ingredientId: 'peanuts',
    ingredientName: 'peanuts',
    kind: 'never',
    reason: 'allergy',
  },
  { id: 'd2', ingredientId: 'coriander', ingredientName: 'coriander', kind: 'avoid', reason: null },
];

const line = (
  ingredientId: string,
  quantity: number | null,
  unit: string | null,
  note: string | null = null,
) => ({
  ingredientId,
  ingredientName: catalog.find((i) => i.id === ingredientId)?.name ?? null,
  ingredientNeedsReview: false,
  quantity,
  quantityPerServing: quantity,
  unit,
  note,
});

const omelette = {
  id: 'omelette',
  name: 'Omelette',
  servings: 2,
  prepTimeMinutes: 10,
  sourceUrl: null,
  tags: ['breakfast'],
  steps: ['Whisk the eggs.', 'Fry.'],
  nutrition: { kcal: null, protein: null, fat: null, carbs: null },
  makeAgain: true,
  averageRating: null,
  ratingCount: 0,
  isFavourite: false,
  cooldownDays: null,
  cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom: null },
  isArchived: false,
  archivedAt: null,
  createdAt: '2026-11-01T10:00:00Z',
  updatedAt: '2026-11-01T10:00:00Z',
  lines: [line('eggs', 4, 'piece')],
  sections: [{ name: 'Topping', lines: [line('onion', 50, 'g', 'chopped')] }],
  dislikes: [],
};

/** The recipe API behind `/api/recipes`; returns the bodies the editor sent. */
async function mockApi(page: Page) {
  const sent: { method: string; body: Record<string, unknown> }[] = [];
  await page.route(/\/api\/catalog\/ingredients(\?.*)?$/, (route) => {
    const search = new URL(route.request().url()).searchParams.get('search')?.toLowerCase() ?? '';
    const items = catalog.filter((i) => i.name.includes(search));
    return route.fulfill({ json: { items, total: items.length, page: 1, pageSize: 50 } });
  });
  await page.route('/api/household/dislikes', (route) => route.fulfill({ json: dislikes }));
  await page.route('/api/household/members', (route) =>
    route.fulfill({ json: { members: [], householdServing: 2 } }),
  );
  await page.route('/api/household/meal-slots', (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/[^/]+\/history$/, (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/tags(\?.*)?$/, (route) =>
    route.fulfill({
      json: [
        { tag: 'quick', recipes: 3 },
        { tag: 'breakfast', recipes: 2 },
      ],
    }),
  );
  await page.route(/\/api\/recipes(\/(?!tags)[^/?]+)?(\?.*)?$/, (route) => {
    const request = route.request();
    const method = request.method();
    if (method === 'GET') {
      return route.fulfill({ json: omelette });
    }
    const body = request.postDataJSON();
    sent.push({ method, body });
    const saved = {
      ...omelette,
      ...body,
      id: method === 'POST' ? 'created' : omelette.id,
      lines: [],
      sections: [],
    };
    return route.fulfill({ status: method === 'POST' ? 201 : 200, json: saved });
  });
  return sent;
}

test.beforeEach(async ({ page }) => {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    route.fulfill({
      json: {
        id: '1',
        displayName: 'Alex',
        role: 'admin',
        userName: null,
        uiLanguage: 'en',
        theme: 'system',
        signInMethods: ['passkey'],
      },
    }),
  );
  await page.addInitScript(() => localStorage.setItem('pantry.uiLanguage', 'en'));
});

const lines = (page: Page) => page.getByTestId('recipe-line');

async function pick(target: Locator, search: string, option: string | RegExp) {
  await target.getByRole('combobox', { name: 'Ingredient', exact: true }).fill(search);
  await target.page().getByRole('option', { name: option }).click();
}

test('creates a recipe typed for its servings, with sections, dislike hints, tags and steps', async ({
  page,
}) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/new');

  await expect(page.getByTestId('recipe-editor-title')).toHaveText('New recipe');
  await page.getByTestId('recipe-name').fill('Peanut noodles');
  await page.getByTestId('recipe-servings').fill('2');
  await expect(page.getByTestId('recipe-ingredients')).toContainText('Quantities for');

  await lines(page).nth(0).getByTestId('line-quantity').fill('1,5');
  await pick(lines(page).nth(0), 'oni', /onion/);
  await expect(lines(page).nth(0).getByTestId('line-ingredient')).toContainText('onion');
  await lines(page).nth(0).getByTestId('line-unit').selectOption('piece');
  await lines(page).nth(0).getByTestId('line-note').fill('sliced');

  await page.getByTestId('add-line').click();
  await pick(lines(page).nth(1), 'peanu', /peanuts/);
  await expect(lines(page).nth(1).getByTestId('hint-never')).toHaveText(
    'On the "never" list (allergy)',
  );
  await lines(page).nth(1).getByTestId('line-quantity').fill('80');

  await page.getByTestId('add-line').click();
  await pick(lines(page).nth(2), 'corian', /coriander/);
  await expect(lines(page).nth(2).getByTestId('hint-avoid')).toBeVisible();

  await page.getByTestId('add-section').click();
  await page.getByTestId('section-name').fill('Sauce');
  const sauce = page.getByTestId('recipe-section');
  await pick(sauce.getByTestId('recipe-line').first(), 'Soy sauce', /Add "Soy sauce"/);
  await expect(sauce.getByTestId('badge-new')).toHaveText('new');
  await expect(sauce.getByTestId('hint-new')).toBeVisible();
  await lines(page).nth(1).getByTestId('line-section').selectOption({ label: 'Sauce' });
  await expect(sauce.getByTestId('line-ingredient')).toHaveText([/Soy sauce/, /peanuts/]);

  await page.getByTestId('recipe-tag-input').fill('Dinner');
  await page.getByTestId('recipe-tag-input').press('Enter');
  await page.getByRole('button', { name: 'Add tag quick' }).click();
  await expect(page.getByTestId('recipe-tag')).toHaveText(['dinner', 'quick']);

  await page.getByTestId('step-text').first().fill('Cook the noodles.');
  await page.getByTestId('add-step').click();
  await page.getByTestId('step-text').nth(1).fill('Make the sauce.');
  await page.getByRole('button', { name: 'Move step 2 up' }).click();
  await page.getByTestId('recipe-prep-time').fill('25');
  await page.getByTestId('nutrition-kcal').fill('610');
  await page.getByTestId('recipe-cooldown').fill('14');
  await page.getByTestId('recipe-make-again').getByRole('switch').click();

  await page.getByTestId('recipe-save').click();

  await expect(page).toHaveURL(/\/recipes\/created$/);
  await expect(page.getByText('Recipe added')).toBeVisible();
  expect(sent).toEqual([
    {
      method: 'POST',
      body: {
        name: 'Peanut noodles',
        servings: 2,
        prepTimeMinutes: 25,
        sourceUrl: null,
        tags: ['dinner', 'quick'],
        steps: ['Make the sauce.', 'Cook the noodles.'],
        nutrition: { kcal: 610, protein: null, fat: null, carbs: null },
        makeAgain: false,
        cooldownDays: 14,
        lines: [
          { ingredientId: 'onion', quantity: 1.5, unit: 'piece', note: 'sliced' },
          { ingredientId: 'coriander', quantity: null, unit: null, note: null },
        ],
        sections: [
          {
            name: 'Sauce',
            lines: [
              { ingredientName: 'Soy sauce', quantity: null, unit: null, note: null },
              { ingredientId: 'peanuts', quantity: 80, unit: 'g', note: null },
            ],
          },
        ],
      },
    },
  ]);
});

test('says what is missing instead of saving', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/new');

  await lines(page).nth(0).getByTestId('line-quantity').fill('2');
  await page.getByTestId('recipe-save').click();

  await expect(page.getByTestId('problems')).toBeVisible();
  await expect(page.getByTestId('problem-name')).toHaveText('Give the recipe a name.');
  await expect(page.getByTestId('problem-ingredient')).toHaveText(
    'Choose an ingredient, or remove the line.',
  );
  expect(sent).toEqual([]);
});

test('edits a recipe and asks before unsaved changes are left', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/omelette/edit');

  await expect(page.getByTestId('recipe-name')).toHaveValue('Omelette');
  await expect(page.getByTestId('recipe-servings')).toHaveValue('2');
  await expect(page.getByTestId('section-name')).toHaveValue('Topping');
  await expect(page.getByTestId('line-quantity').nth(0)).toHaveValue('4');
  await expect(page.getByTestId('line-quantity').nth(1)).toHaveValue('50');
  await expect(page.getByTestId('recipe-save')).toBeDisabled();

  await page.getByTestId('line-quantity').first().fill('6');
  await page.getByTestId('remove-section').click();
  await expect(page.getByTestId('recipe-section')).toHaveCount(0);
  await expect(page.getByTestId('recipe-save')).toBeEnabled();

  await page.getByTestId('recipe-back').click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Changes to Omelette have not been saved.');
  await dialog.getByRole('button', { name: 'Keep editing' }).click();
  await expect(page).toHaveURL(/\/recipes\/omelette\/edit$/);

  await page.getByTestId('recipe-save').click();
  await expect(page.getByText('Recipe saved')).toBeVisible();
  expect(sent).toHaveLength(1);
  expect(sent[0].method).toBe('PUT');
  expect(sent[0].body).toMatchObject({
    name: 'Omelette',
    servings: 2,
    lines: [
      { ingredientId: 'eggs', quantity: 6, unit: 'piece', note: null },
      { ingredientId: 'onion', quantity: 50, unit: 'g', note: 'chopped' },
    ],
    sections: [],
  });

  await expect(page).toHaveURL(/\/recipes\/omelette$/);
  await expect(page.getByTestId('recipe-title')).toHaveText('Omelette');
});

test('cancel goes back to the recipe, or to the library for a new one', async ({ page }) => {
  await mockApi(page);
  await page.goto('/recipes/omelette/edit');
  await expect(page.getByTestId('recipe-name')).toHaveValue('Omelette');
  await page.getByRole('button', { name: 'Cancel' }).click();
  await expect(page).toHaveURL(/\/recipes\/omelette$/);

  await page.goto('/recipes/new');
  await page.getByTestId('recipe-back').click();
  await expect(page).toHaveURL(/\/recipes$/);
});
