import { expect, Page, test } from '@playwright/test';

/** A 1x1 PNG, enough for the browser to show and for the editor to send. */
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
);

const photoOf = (id: string, photoId: string) => ({
  card: `/api/recipes/${id}/photos/${photoId}/card`,
  detail: `/api/recipes/${id}/photos/${photoId}/detail`,
});

const soup = {
  id: 'soup',
  name: 'Tomato soup',
  servings: 2,
  prepTimeMinutes: 30,
  sourceUrl: null,
  tags: [],
  steps: [],
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
  photo: null as ReturnType<typeof photoOf> | null,
  lines: [],
  sections: [],
  dislikes: [],
};

type Sent = { method: string; path: string; contentType?: string; bytes?: number };

/** The recipe API with one recipe that is `soup` until something is created; returns what the app sent. */
async function mockApi(page: Page, recipe = soup) {
  const sent: Sent[] = [];
  let current = { ...recipe };
  let uploads = 0;
  await page.route('/api/household/dislikes', (route) => route.fulfill({ json: [] }));
  await page.route('/api/household/members', (route) =>
    route.fulfill({ json: { members: [], householdServing: 2 } }),
  );
  await page.route('/api/household/meal-slots', (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/tags(\?.*)?$/, (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/history\/to-rate(\?.*)?$/, (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/[^/]+\/history$/, (route) => route.fulfill({ json: [] }));
  await page.route(/\/api\/recipes\/[^/]+\/photos\/.+$/, (route) =>
    route.fulfill({ contentType: 'image/png', body: png }),
  );
  await page.route(/\/api\/recipes\/[^/]+\/photo$/, (route) => {
    const request = route.request();
    sent.push({
      method: request.method(),
      path: 'photo',
      contentType: request.headers()['content-type'],
      bytes: request.postDataBuffer()?.length,
    });
    current = {
      ...current,
      photo: request.method() === 'PUT' ? photoOf(current.id, `p${++uploads}`) : null,
    };
    return route.fulfill({ json: current });
  });
  await page.route(/\/api\/recipes(\?.*)?$/, (route) => {
    const request = route.request();
    if (request.method() === 'GET') {
      return route.fulfill({ json: { items: [current], total: 1, page: 1, pageSize: 50 } });
    }
    sent.push({ method: 'POST', path: 'recipe' });
    current = { ...current, ...request.postDataJSON(), id: 'created', photo: null };
    return route.fulfill({ status: 201, json: current });
  });
  await page.route(/\/api\/recipes\/[^/?]+$/, (route) => {
    const request = route.request();
    if (request.method() === 'PUT') {
      sent.push({ method: 'PUT', path: 'recipe' });
      current = { ...current, ...request.postDataJSON() };
    }
    return route.fulfill({ json: current });
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

test('adds a photo to a new recipe and uploads it once the recipe is saved', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/new');

  await page.getByTestId('recipe-name').fill('Tomato soup');
  await expect(page.getByTestId('recipe-photo-drop')).toContainText('Drop a photo or tap to choose');
  await page
    .getByTestId('recipe-photo-input')
    .setInputFiles({ name: 'soup.png', mimeType: 'image/png', buffer: png });
  await expect(page.getByTestId('recipe-photo-preview')).toBeVisible();
  expect(sent).toEqual([]);

  await page.getByRole('button', { name: 'Add recipe' }).click();

  await expect(page).toHaveURL(/\/recipes\/created$/);
  expect(sent).toEqual([
    { method: 'POST', path: 'recipe' },
    { method: 'PUT', path: 'photo', contentType: 'image/png', bytes: png.length },
  ]);
  await expect(page.getByTestId('recipe-title')).toHaveText('Tomato soup');
  await expect(page.getByTestId('recipe-photo').last()).toHaveAttribute(
    'src',
    '/api/recipes/created/photos/p1/detail',
  );
});

test('drops a photo on the field', async ({ page }) => {
  await mockApi(page);
  await page.goto('/recipes/soup/edit');

  const dataTransfer = await page.evaluateHandle((bytes) => {
    const transfer = new DataTransfer();
    transfer.items.add(new File([new Uint8Array(bytes)], 'soup.png', { type: 'image/png' }));
    return transfer;
  }, [...png]);
  await page.getByTestId('recipe-photo-drop').dispatchEvent('drop', { dataTransfer });

  await expect(page.getByTestId('recipe-photo-preview')).toBeVisible();
  await expect(page.getByText('Saved with the recipe.')).toBeVisible();
});

test('replaces and removes the photo of a recipe', async ({ page }) => {
  const sent = await mockApi(page, { ...soup, photo: photoOf('soup', 'old') });
  await page.goto('/recipes/soup/edit');

  await expect(page.getByTestId('recipe-photo-preview')).toHaveAttribute(
    'src',
    '/api/recipes/soup/photos/old/detail',
  );
  await page.getByTestId('recipe-photo-remove').click();
  await expect(page.getByTestId('recipe-photo-removed')).toContainText('The photo is removed when you save.');
  await page.getByTestId('recipe-photo-undo').click();
  await expect(page.getByTestId('recipe-photo-preview')).toBeVisible();

  await page
    .getByTestId('recipe-photo-input')
    .setInputFiles({ name: 'new.png', mimeType: 'image/png', buffer: png });
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page).toHaveURL(/\/recipes\/soup$/);
  expect(sent).toEqual([
    { method: 'PUT', path: 'recipe' },
    { method: 'PUT', path: 'photo', contentType: 'image/png', bytes: png.length },
  ]);
  await expect(page.getByTestId('recipe-photo').last()).toHaveAttribute('src', '/api/recipes/soup/photos/p1/detail');

  await page.getByTestId('recipe-edit').click();
  await page.getByTestId('recipe-photo-remove').click();
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page).toHaveURL(/\/recipes\/soup$/);
  expect(sent.slice(2)).toEqual([
    { method: 'PUT', path: 'recipe' },
    { method: 'DELETE', path: 'photo', contentType: undefined, bytes: undefined },
  ]);
  await expect(page.getByTestId('recipe-title')).toBeVisible();
  await expect(page.locator('img[data-testid="recipe-photo"]')).toHaveCount(0);
});

test('refuses a file that is not a JPEG, PNG or WebP picture', async ({ page }) => {
  const sent = await mockApi(page);
  await page.goto('/recipes/soup/edit');

  await page
    .getByTestId('recipe-photo-input')
    .setInputFiles({ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('soup') });

  await expect(page.getByTestId('recipe-photo-problem')).toHaveText('Choose a JPEG, PNG or WebP picture.');
  await expect(page.getByTestId('recipe-photo-preview')).toHaveCount(0);
  await expect(page.getByTestId('recipe-save')).toBeDisabled();
  expect(sent).toEqual([]);
});
