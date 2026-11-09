import { expect, test } from '@playwright/test';

const destinations: { name: string; path: string | RegExp; heading?: string }[] = [
  { name: 'Plan', path: '/planner', heading: 'Plan this week' },
  { name: 'Recipes', path: '/recipes', heading: 'Add your first recipe' },
  { name: 'Shopping', path: '/shopping', heading: 'Everything is in the cart' },
  { name: 'Household', path: /\/household(\/members)?$/ },
];

test.beforeEach(async ({ page }) => {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/household/members', (route) => route.fulfill({ json: { members: [], householdServing: 0 } }));
  await page.addInitScript(() => {
    if (!sessionStorage.getItem('e2e.seeded')) {
      localStorage.setItem('pantry.uiLanguage', 'en');
      sessionStorage.setItem('e2e.seeded', '1');
    }
  });
});

test('opens the planner and navigates all four destinations', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL('/planner');

  const navigation = page.getByRole('navigation', { name: 'Main navigation' });
  for (const destination of destinations) {
    const link = navigation.getByRole('link', { name: destination.name });
    await link.click();

    await expect(page).toHaveURL(destination.path);
    await expect(link).toHaveAttribute('aria-current', 'page');
    if (destination.heading) {
      await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveText(destination.heading);
    }
  }
});

test('shows the saved sync state', async ({ page }) => {
  await page.goto('/planner');

  await expect(page.getByRole('status').filter({ visible: true })).toContainText('Saved');
});

test('remembers the UI language chosen in the avatar menu', async ({ page }) => {
  await page.goto('/recipes');

  await page.getByRole('button', { name: 'Account' }).filter({ visible: true }).click();
  await page.getByRole('dialog', { name: 'Account' }).getByTestId('language-pl').click();
  await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Dodaj pierwszy przepis');
  await expect(page).toHaveTitle('Spiżarnia');

  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toHaveCount(0);

  await page.reload();
  await expect(page.getByRole('navigation', { name: 'Nawigacja główna' }).getByRole('link', { name: 'Przepisy' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  await expect(page.locator('html')).toHaveAttribute('lang', 'pl');
});

test('follows the system theme until a theme is chosen', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await page.getByRole('button', { name: 'Account' }).filter({ visible: true }).click();
  await page.getByTestId('theme-light').click();
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('shows the design tokens page outside the shell', async ({ page }) => {
  await page.goto('/design-system');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Design tokens');
  await expect(page.getByRole('navigation')).toHaveCount(0);
});
