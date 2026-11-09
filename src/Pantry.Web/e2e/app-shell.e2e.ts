import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.route('/api/health', (route) => route.fulfill({ json: { status: 'ok', version: 'e2e' } }));
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
});

test('shows the app and the API status', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Pantry');
  await expect(page.getByTestId('api-status')).toContainText('ok');
});

test('remembers the chosen UI language', async ({ page }) => {
  await page.goto('/');

  await page.getByTestId('language-pl').click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Spiżarnia');
  await expect(page).toHaveTitle('Spiżarnia');

  await page.reload();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Spiżarnia');
  await expect(page.locator('html')).toHaveAttribute('lang', 'pl');
});

test('follows the system theme until a theme is chosen', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await page.getByTestId('theme-light').click();
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
});

test('shows the design tokens page', async ({ page }) => {
  await page.goto('/design-system');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Design tokens');
});
