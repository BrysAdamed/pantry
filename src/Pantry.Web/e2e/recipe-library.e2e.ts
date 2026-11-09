import { expect, Page, test } from '@playwright/test';

const today = new Date();
const daysFromToday = (days: number) => {
  const date = new Date(today);
  date.setDate(date.getDate() + days);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
};

const recipe = (id: string, name: string, change: Record<string, unknown> = {}) => ({
  id,
  name,
  servings: 2,
  prepTimeMinutes: 30,
  tags: [] as string[],
  nutrition: { kcal: null, protein: null, fat: null, carbs: null },
  makeAgain: true,
  averageRating: null,
  ratingCount: 0,
  isFavourite: false,
  cooldownDays: null,
  cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom: null },
  isArchived: false,
  updatedAt: '2026-11-01T10:00:00Z',
  dislikes: [] as { kind: string }[],
  ...change,
});

const library = [
  recipe('curry', 'Chicken curry', {
    tags: ['dinner'],
    averageRating: 4.5,
    ratingCount: 2,
    ingredients: ['chicken', 'rice', 'peanuts'],
    cooldown: { days: 21, isHouseholdDefault: true, lastEaten: daysFromToday(-2), availableFrom: daysFromToday(19) },
    dislikes: [{ ingredientId: 'peanuts', ingredientName: 'peanuts', kind: 'never', reason: 'allergy' }],
  }),
  recipe('lecso', 'Courgette lecso', { tags: ['dinner', 'quick'], averageRating: 4, ratingCount: 1, ingredients: ['courgette'] }),
  recipe('porridge', 'Porridge', { tags: ['breakfast', 'quick'], ingredients: ['oats', 'apple'] }),
  recipe('soup', 'Mushroom soup', { tags: ['dinner'], isArchived: true, ingredients: ['mushrooms'] }),
];

/** `GET /api/recipes` over the sample library: search over names, tags and ingredients, tags, without never, archive. */
async function mockLibrary(page: Page, recipes = library) {
  const queries: URLSearchParams[] = [];
  await page.route(/\/api\/recipes\/tags(\?.*)?$/, (route) =>
    route.fulfill({
      json: [
        { tag: 'dinner', recipes: 2 },
        { tag: 'quick', recipes: 2 },
        { tag: 'breakfast', recipes: 1 },
      ],
    }),
  );
  await page.route(/\/api\/recipes(\?.*)?$/, (route) => {
    const params = new URL(route.request().url()).searchParams;
    if (params.get('pageSize') !== '1') {
      queries.push(params);
    }
    const search = params.get('search')?.toLowerCase();
    const tags = params.getAll('tag');
    const items = recipes
      .filter((r) => r.isArchived === (params.get('archived') === 'true'))
      .filter(
        (r) =>
          !search ||
          [r.name, ...r.tags, ...((r['ingredients'] as string[] | undefined) ?? [])].some((text) => text.toLowerCase().includes(search)),
      )
      .filter((r) => tags.every((tag) => r.tags.includes(tag)))
      .filter((r) => params.get('withoutNever') !== 'true' || !r.dislikes.some((d) => d.kind === 'never'))
      .map(({ ingredients: _, ...item }) => item);
    return route.fulfill({ json: { items, total: items.length, page: 1, pageSize: Number(params.get('pageSize')) } });
  });
  return queries;
}

test.beforeEach(async ({ page }) => {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    route.fulfill({
      json: { id: '1', displayName: 'Alex', role: 'admin', userName: null, uiLanguage: 'en', theme: 'system', signInMethods: ['passkey'] },
    }),
  );
  await page.addInitScript(() => localStorage.setItem('pantry.uiLanguage', 'en'));
});

const names = (page: Page) => page.getByTestId('recipe-name');

test('lists recipes with their badges and searches names, tags and ingredients', async ({ page }) => {
  const queries = await mockLibrary(page);
  await page.goto('/recipes');

  await expect(names(page)).toHaveText(['Chicken curry', 'Courgette lecso', 'Porridge']);
  await expect(page.getByTestId('recipe-count')).toHaveText('3 recipes');
  const curry = page.getByTestId('recipe-card').filter({ hasText: 'Chicken curry' });
  await expect(curry.getByTestId('badge-never')).toHaveText('never');
  await expect(curry.getByTestId('badge-too-soon')).toHaveText('too soon');
  await expect(curry.getByTestId('recipe-last-eaten')).toHaveText('2 days ago');
  await expect(curry.getByRole('img', { name: 'Rated 4.5 out of 5' })).toBeVisible();
  await expect(curry).toHaveAttribute('href', '/recipes/curry');

  await page.getByTestId('recipe-search').fill('apple');
  await expect(names(page)).toHaveText(['Porridge']);
  await expect(page.getByTestId('recipe-count')).toHaveText('1 recipe');
  expect(queries.at(-1)?.get('search')).toBe('apple');

  await page.getByTestId('recipe-search').fill('quick');
  await expect(names(page)).toHaveText(['Courgette lecso', 'Porridge']);
});

test('filters by tags, without never and the archive, and sorts', async ({ page }) => {
  const queries = await mockLibrary(page);
  await page.goto('/recipes');
  await expect(names(page)).toHaveCount(3);
  expect(queries.at(-1)?.get('sort')).toBe('rating');

  await page.getByTestId('filter-tag').filter({ hasText: 'dinner' }).click();
  await expect(names(page)).toHaveText(['Chicken curry', 'Courgette lecso']);
  await page.getByTestId('filter-tag').filter({ hasText: 'quick' }).click();
  await expect(names(page)).toHaveText(['Courgette lecso']);
  expect(queries.at(-1)?.getAll('tag')).toEqual(['dinner', 'quick']);

  await page.getByTestId('filter-tag').filter({ hasText: 'quick' }).click();
  await page.getByTestId('filter-without-never').click();
  await expect(names(page)).toHaveText(['Courgette lecso']);
  await expect(page.getByTestId('filter-without-never')).toHaveAttribute('aria-pressed', 'true');

  await page.getByTestId('recipe-sort').selectOption({ label: 'Not eaten lately' });
  await expect.poll(() => queries.at(-1)?.get('sort')).toBe('notEatenLately');

  await page.getByTestId('filter-archived').click();
  await expect(names(page)).toHaveText(['Mushroom soup']);
  await expect(page.getByTestId('recipe-count')).toHaveText('1 archived recipe');
});

test('says when nothing matches and clears the filters', async ({ page }) => {
  await mockLibrary(page);
  await page.goto('/recipes');

  await page.getByTestId('recipe-search').fill('pierogi');
  await expect(page.getByRole('heading', { name: 'Nothing matches' })).toBeVisible();
  await page.getByRole('button', { name: 'Clear filters' }).click();
  await expect(page.getByTestId('recipe-search')).toHaveValue('');
  await expect(names(page)).toHaveCount(3);
});

test('welcomes an empty library and opens the editor from it', async ({ page }) => {
  await mockLibrary(page, []);
  await page.route('/api/household/dislikes', (route) => route.fulfill({ json: [] }));
  await page.goto('/recipes');

  const welcome = page.getByRole('heading', { name: 'Add your first recipe' });
  await expect(welcome).toBeVisible();
  await page.getByTestId('new-recipe').click();
  await expect(page).toHaveURL('/recipes/new');
  await expect(page.getByTestId('recipe-editor-title')).toHaveText('New recipe');
});
