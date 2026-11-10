import { expect, Page, test } from '@playwright/test';

const line = (
  ingredientId: string,
  quantityPerServing: number | null,
  unit: string | null,
  note: string | null = null,
) => ({
  ingredientId,
  ingredientName: ingredientId,
  ingredientNeedsReview: false,
  quantity: quantityPerServing === null ? null : quantityPerServing * 2,
  quantityPerServing,
  unit,
  note,
});

const isoDay = (offset: number) => {
  const day = new Date();
  day.setDate(day.getDate() + offset);
  return `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
};

const curry = {
  id: 'curry',
  name: 'Chicken curry',
  servings: 2,
  prepTimeMinutes: 70,
  sourceUrl: 'https://example.com/curry',
  tags: ['dinner', 'chicken'],
  steps: ['Cook the rice.', 'Fry the chicken.'],
  nutrition: { kcal: 640, protein: 38, fat: null, carbs: 70 },
  makeAgain: true,
  averageRating: 4.5,
  ratingCount: 2,
  isFavourite: true,
  cooldownDays: null,
  cooldown: { days: 21, isHouseholdDefault: true, lastEaten: isoDay(-2), availableFrom: isoDay(19) },
  isArchived: false,
  archivedAt: null as string | null,
  createdAt: '2026-11-01T10:00:00Z',
  updatedAt: '2026-11-01T10:00:00Z',
  lines: [line('chicken', 150, 'g'), line('garlic', 1, 'clove', 'crushed'), line('salt', null, null)],
  sections: [{ name: 'Topping', lines: [line('coriander', 2, 'g'), line('peanuts', 15, 'g')] }],
  dislikes: [
    { ingredientId: 'peanuts', ingredientName: 'peanuts', kind: 'never', reason: 'allergy' },
    { ingredientId: 'coriander', ingredientName: 'coriander', kind: 'avoid', reason: null },
  ],
};

const history = [
  { id: 'h1', recipeId: 'curry', date: isoDay(-2), mealSlotId: 'dinner', planEntryId: 'p1', rating: 5 },
  { id: 'h2', recipeId: 'curry', date: isoDay(-30), mealSlotId: 'dinner', planEntryId: 'p2', rating: 4 },
  { id: 'h3', recipeId: 'curry', date: isoDay(-60), mealSlotId: 'lunch', planEntryId: 'p3', rating: null },
];

/** The API behind the detail; returns the requests that changed something. */
async function mockApi(page: Page, recipe: object = curry) {
  const sent: { method: string; url: string; body: unknown }[] = [];
  let current = { ...recipe } as typeof curry;
  await page.route('/api/household/members', (route) =>
    route.fulfill({ json: { members: [], householdServing: 2.3 } }),
  );
  await page.route('/api/household/meal-slots', (route) =>
    route.fulfill({
      json: [
        { id: 'lunch', name: 'Lunch', position: 0, isEnabled: true },
        { id: 'dinner', name: 'Dinner', position: 1, isEnabled: true },
      ],
    }),
  );
  await page.route(/\/api\/recipes\/[^/]+\/history$/, (route) => route.fulfill({ json: history }));
  await page.route(/\/api\/recipes\/[^/]+\/make-again$/, (route) => {
    const body = route.request().postDataJSON();
    sent.push({ method: 'PUT', url: 'make-again', body });
    current = { ...current, makeAgain: body.makeAgain, isFavourite: body.makeAgain };
    return route.fulfill({
      json: {
        recipeId: current.id,
        makeAgain: body.makeAgain,
        averageRating: 4.5,
        ratingCount: 2,
        isFavourite: body.makeAgain,
      },
    });
  });
  await page.route(/\/api\/recipes\/[^/]+\/(archive|restore)$/, (route) => {
    const archive = route.request().url().endsWith('/archive');
    sent.push({ method: 'POST', url: archive ? 'archive' : 'restore', body: null });
    current = { ...current, isArchived: archive, archivedAt: archive ? '2026-11-09T10:00:00Z' : null };
    return route.fulfill({ json: current });
  });
  await page.route(/\/api\/recipes\/[^/?]+$/, (route) =>
    route.request().url().endsWith('/missing')
      ? route.fulfill({ status: 404, json: {} })
      : route.fulfill({ json: current }),
  );
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

test('shows a recipe scaled to the household with warnings, steps, nutrition and history', async ({ page }) => {
  await mockApi(page);
  await page.goto('/recipes/curry');

  await expect(page.getByTestId('recipe-title')).toHaveText('Chicken curry');
  await expect(page.getByTestId('recipe-time')).toHaveText('1 h 10 min');
  await expect(page.getByTestId('recipe-rating')).toContainText('4.5');
  await expect(page.getByTestId('recipe-last-eaten')).toContainText('Last eaten');
  await expect(page.getByTestId('recipe-source')).toHaveAttribute('href', 'https://example.com/curry');
  await expect(page.getByTestId('recipe-tag')).toHaveText(['dinner', 'chicken']);
  await expect(page.getByTestId('badge-favourite')).toBeVisible();
  await expect(page.getByTestId('badge-avoid')).toHaveText('avoid: coriander');
  await expect(page.getByTestId('badge-too-soon')).toContainText('too soon, best from');
  await expect(page.getByTestId('never-alert')).toContainText('Contains peanuts');
  await expect(page.getByTestId('never-alert')).toContainText('(allergy)');

  await expect(page.getByTestId('servings')).toHaveText('2.3');
  await expect(page.getByTestId('servings-note')).toHaveText('For the household (2.3 servings).');
  const quantities = page.getByTestId('line-quantity');
  await expect(quantities).toHaveText(['345 g', '2.5 cloves', 'to taste', '5 g', '35 g']);
  await expect(page.getByTestId('section-title')).toHaveText('Topping');
  await expect(page.getByTestId('ingredient-line').nth(1)).toContainText('garlic, crushed');
  await expect(page.getByTestId('line-dislike')).toHaveText(['avoid', 'never']);

  await page.getByTestId('servings-up').click();
  await expect(page.getByTestId('servings')).toHaveText('2.5');
  await page.getByTestId('servings-up').click();
  await expect(page.getByTestId('servings')).toHaveText('3');
  await expect(page.getByTestId('servings-note')).toContainText('Scaled to 3 servings.');
  await expect(quantities).toHaveText(['450 g', '3 cloves', 'to taste', '6 g', '45 g']);
  await page.getByTestId('servings-reset').click();
  await expect(page.getByTestId('servings')).toHaveText('2.3');

  await expect(page.getByTestId('recipe-step')).toHaveText(['1Cook the rice.', '2Fry the chicken.']);
  await expect(page.getByTestId('nutrient')).toContainText(['kcal 640', 'protein 38 g', 'fat –', 'carbs 70 g']);

  await expect(page.getByTestId('average-rating')).toHaveText('4.5');
  await expect(page.getByTestId('history-entry')).toHaveCount(3);
  await expect(page.getByTestId('history-entry').first()).toContainText('Dinner');
  await expect(page.getByTestId('history-entry').nth(2)).toContainText('not rated');
});

test('turns make again off and archives and restores the recipe', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/curry');

  const makeAgain = page.getByTestId('make-again').getByRole('switch');
  await expect(makeAgain).toHaveAttribute('aria-checked', 'true');
  await makeAgain.click();
  await expect(makeAgain).toHaveAttribute('aria-checked', 'false');
  await expect(page.getByTestId('make-again-note')).toHaveText('Not again; the agent skips it.');
  await expect(page.getByTestId('badge-favourite')).toHaveCount(0);

  await page.getByTestId('recipe-archive').click();
  await expect(page.getByTestId('archived-banner')).toBeVisible();
  await expect(page.getByText('Recipe archived')).toBeVisible();
  await expect(page.getByTestId('recipe-plan')).toHaveCount(0);

  await page.getByTestId('recipe-restore').click();
  await expect(page.getByTestId('archived-banner')).toHaveCount(0);
  await expect(page.getByTestId('recipe-archive')).toBeVisible();

  expect(sent).toEqual([
    { method: 'PUT', url: 'make-again', body: { makeAgain: false } },
    { method: 'POST', url: 'archive', body: null },
    { method: 'POST', url: 'restore', body: null },
  ]);
});

test('says when there is no nutrition or history, and links to the editor', async ({ page }) => {
  await mockApi(page, {
    ...curry,
    nutrition: { kcal: null, protein: null, fat: null, carbs: null },
    averageRating: null,
    ratingCount: 0,
    isFavourite: false,
    dislikes: [],
    cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom: null },
  });
  await page.route(/\/api\/recipes\/[^/]+\/history$/, (route) => route.fulfill({ json: [] }));
  await page.goto('/recipes/curry');

  await expect(page.getByTestId('no-nutrition')).toBeVisible();
  await expect(page.getByTestId('no-history')).toBeVisible();
  await expect(page.getByTestId('recipe-last-eaten')).toHaveText('Not eaten yet');
  await expect(page.getByTestId('recipe-rating')).toHaveText('Not rated yet');
  await expect(page.getByTestId('never-alert')).toHaveCount(0);
  await expect(page.getByTestId('badge-too-soon')).toHaveCount(0);

  await expect(page.getByTestId('recipe-edit')).toHaveAttribute('href', '/recipes/curry/edit');
});

test('says when the recipe does not exist', async ({ page }) => {
  await mockApi(page);
  await page.goto('/recipes/missing');
  await expect(page.getByTestId('recipe-gone')).toHaveText('This recipe does not exist.');
});
