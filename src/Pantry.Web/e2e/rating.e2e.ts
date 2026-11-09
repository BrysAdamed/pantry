import { expect, Page, test } from '@playwright/test';

const isoDay = (offset: number) => {
  const day = new Date();
  day.setDate(day.getDate() + offset);
  return `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
};

const summary = (change: object = {}) => ({
  recipeId: 'curry',
  makeAgain: true,
  averageRating: 4,
  ratingCount: 1,
  isFavourite: true,
  ...change,
});

const curry = {
  id: 'curry',
  name: 'Chicken curry',
  servings: 2,
  prepTimeMinutes: 30,
  sourceUrl: null,
  tags: [],
  steps: [],
  nutrition: { kcal: null, protein: null, fat: null, carbs: null },
  makeAgain: true,
  averageRating: 5,
  ratingCount: 1,
  isFavourite: true,
  cooldownDays: null,
  cooldown: { days: 0, isHouseholdDefault: true, lastEaten: isoDay(-1), availableFrom: isoDay(-1) },
  isArchived: false,
  archivedAt: null,
  createdAt: '2026-11-01T10:00:00Z',
  updatedAt: '2026-11-01T10:00:00Z',
  lines: [],
  sections: [],
  dislikes: [],
};

interface Sent {
  method: string;
  entryId: string;
  body: unknown;
}

/** Ratings and to rate in memory; returns the rating writes. */
async function mockApi(page: Page) {
  const sent: Sent[] = [];
  const history = [
    { id: 'h1', recipeId: 'curry', date: isoDay(-1), mealSlotId: 'dinner', planEntryId: 'p1', rating: 5 as number | null },
    { id: 'h2', recipeId: 'curry', date: isoDay(-9), mealSlotId: 'lunch', planEntryId: 'p2', rating: null as number | null },
  ];
  let toRate = [
    { id: 't1', recipeId: 'soup', recipeName: 'Tomato soup', recipeIsArchived: false, date: isoDay(-1), mealSlotId: 'dinner', planEntryId: 'p3' },
    { id: 't2', recipeId: 'stew', recipeName: 'Lentil stew', recipeIsArchived: false, date: isoDay(-5), mealSlotId: 'lunch', planEntryId: 'p4' },
  ];
  await page.route('/api/household/members', (route) =>
    route.fulfill({ json: { members: [], householdServing: 2 } }),
  );
  await page.route('/api/household/meal-slots', (route) =>
    route.fulfill({
      json: [
        { id: 'lunch', name: 'Lunch', position: 0, isEnabled: true },
        { id: 'dinner', name: 'Dinner', position: 1, isEnabled: true },
      ],
    }),
  );
  await page.route(/\/api\/recipes\/[^/?]+$/, (route) => route.fulfill({ json: curry }));
  await page.route(/\/api\/recipes\/curry\/history$/, (route) => route.fulfill({ json: history }));
  await page.route('/api/recipes/history/to-rate', (route) => route.fulfill({ json: toRate }));
  await page.route(/\/api\/recipes\/history\/[^/]+\/rating$/, (route) => {
    const request = route.request();
    const entryId = request.url().split('/').at(-2)!;
    const method = request.method();
    const body = method === 'PUT' ? request.postDataJSON() : null;
    sent.push({ method, entryId, body });
    const stars = method === 'PUT' ? body.stars : null;
    toRate = toRate.filter((t) => t.id !== entryId);
    const entry = history.find((h) => h.id === entryId);
    if (entry) {
      entry.rating = stars;
    }
    return route.fulfill({
      json: {
        entry: { id: entryId, recipeId: 'curry', date: isoDay(-1), mealSlotId: 'dinner', planEntryId: 'p', rating: stars },
        recipe: summary(stars === null ? { averageRating: null, ratingCount: 0, isFavourite: false } : { averageRating: stars }),
      },
    });
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

test('rates a dish from the to rate cards in two taps, and later keeps it waiting', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes');

  const cards = page.getByTestId('to-rate-card');
  await expect(cards).toHaveCount(2);
  await expect(cards.first()).toContainText('Tomato soup');
  await expect(cards.first()).toContainText('Yesterday · Dinner');

  await cards.nth(1).getByTestId('to-rate-rate').click();
  const sheet = page.getByRole('dialog');
  await expect(sheet.getByTestId('rating-recipe')).toHaveText('Lentil stew');
  await sheet.getByTestId('rating-later').click();
  await expect(sheet).toHaveCount(0);
  await expect(cards).toHaveCount(2);

  await cards.first().getByTestId('to-rate-rate').click();
  await expect(sheet.getByTestId('rating-save')).toBeDisabled();
  await sheet.getByRole('radio', { name: '4 out of 5, Very good' }).click();
  await expect(sheet.getByTestId('star-word')).toHaveText('Very good');
  await sheet.getByTestId('make-again-no').click();
  await expect(sheet.getByTestId('make-again-hint')).toBeVisible();
  await sheet.getByTestId('rating-save').click();

  await expect(sheet).toHaveCount(0);
  await expect(page.getByText('Thanks! Rating saved')).toBeVisible();
  await expect(cards).toHaveCount(1);
  await expect(cards.first()).toContainText('Lentil stew');
  expect(sent).toEqual([{ method: 'PUT', entryId: 't1', body: { stars: 4, makeAgain: false } }]);
});

test('rates, changes and removes ratings from the recipe history', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/curry');

  const entries = page.getByTestId('history-entry');
  await expect(entries).toHaveCount(2);

  await entries.nth(1).getByTestId('history-rate').click();
  const sheet = page.getByRole('dialog');
  await expect(sheet.getByTestId('rating-when')).toContainText('Lunch');
  await expect(sheet.getByTestId('rating-remove')).toHaveCount(0);
  await expect(sheet.getByTestId('make-again-yes')).toHaveAttribute('aria-pressed', 'true');
  await sheet.getByTestId('star-3').click();
  await sheet.getByTestId('rating-save').click();
  await expect(sheet).toHaveCount(0);
  await expect(entries.nth(1)).toContainText('★★★☆☆');
  await expect(page.getByTestId('average-rating')).toHaveText('3');

  await entries.first().getByTestId('history-rate').click();
  await expect(sheet.getByTestId('star-5')).toHaveAttribute('aria-checked', 'true');
  await sheet.getByTestId('rating-remove').click();
  await expect(sheet).toHaveCount(0);
  await expect(page.getByText('Rating removed')).toBeVisible();
  await expect(entries.first()).toContainText('not rated');

  expect(sent).toEqual([
    { method: 'PUT', entryId: 'h2', body: { stars: 3, makeAgain: true } },
    { method: 'DELETE', entryId: 'h1', body: null },
  ]);
});
