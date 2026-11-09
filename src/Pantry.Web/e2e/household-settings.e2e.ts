import { expect, Page, test } from '@playwright/test';

interface Member {
  id: string;
  name: string;
  portionFactor: number;
  position: number;
  account: 'admin' | 'user' | null;
}

const sections = ['Members', 'Meals', "We don't eat", 'Planning rules', 'Content language', 'Access tokens', 'My account'];

async function mockHousehold(page: Page, language: 'en' | 'pl' = 'en') {
  const members: Member[] = [
    { id: 'a', name: 'Alex', portionFactor: 1, position: 0, account: 'admin' },
    { id: 't', name: 'Toddler', portionFactor: 0.3, position: 1, account: null },
  ];
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    route.fulfill({ json: { id: '1', displayName: 'Alex', role: 'admin', uiLanguage: null, theme: 'system' } }),
  );
  await page.route('/api/household/members**', async (route) => {
    const request = route.request();
    const id = new URL(request.url()).pathname.split('/')[4];
    if (request.method() === 'GET') {
      return route.fulfill({ json: { members, householdServing: members.reduce((s, m) => s + m.portionFactor, 0) } });
    }
    if (request.method() === 'POST') {
      const member: Member = { id: `m${members.length}`, position: members.length, account: null, ...request.postDataJSON() };
      members.push(member);
      return route.fulfill({ status: 201, json: member });
    }
    const index = members.findIndex((m) => m.id === id);
    if (request.method() === 'PUT') {
      members[index] = { ...members[index], ...request.postDataJSON() };
      return route.fulfill({ json: members[index] });
    }
    members.splice(index, 1);
    return route.fulfill({ status: 204 });
  });
  await page.addInitScript((lang) => localStorage.setItem('pantry.uiLanguage', lang), language);
}

test('shows the section list next to the open section on wide screens', async ({ page, isMobile }) => {
  test.skip(isMobile, 'Desktop layout');
  await mockHousehold(page);

  await page.goto('/household');

  await expect(page).toHaveURL('/household/members');
  const list = page.getByRole('navigation', { name: 'Household settings' });
  await expect(list.getByRole('link')).toHaveText(sections.map((name) => new RegExp(`^${name}`)));
  await expect(list.getByRole('link', { name: /^Members/ })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('heading', { level: 2, name: 'Members' })).toBeVisible();

  await list.getByRole('link', { name: /^Meals/ }).click();
  await expect(page).toHaveURL('/household/slots');
  await expect(page.getByRole('heading', { level: 2, name: 'Meals' })).toBeVisible();
  await expect(page.getByTestId('section-placeholder')).toBeVisible();
  await expect(list).toBeVisible();
});

test('drills down from the section list on phones', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Phone layout');
  await mockHousehold(page);

  await page.goto('/household');

  const list = page.getByRole('navigation', { name: 'Household settings' });
  await expect(list.getByRole('link')).toHaveCount(sections.length);
  await list.getByRole('link', { name: /Members/ }).click();

  await expect(page).toHaveURL('/household/members');
  await expect(list).toBeHidden();
  await expect(page.getByRole('heading', { level: 2, name: 'Members' })).toBeVisible();

  await page.getByTestId('settings-back').click();
  await expect(page).toHaveURL('/household');
  await expect(list).toBeVisible();
});

test('updates the household serving live and adds a member', async ({ page }) => {
  await mockHousehold(page);
  await page.goto('/household/members');
  const serving = page.getByTestId('household-serving');
  await expect(serving).toHaveText('1.3');
  await expect(page.getByText('admin', { exact: true })).toBeVisible();
  await expect(page.getByText('no account', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'More for Toddler' }).click();
  await page.getByRole('button', { name: 'More for Toddler' }).click();
  await expect(serving).toHaveText('1.4');

  await page.getByRole('textbox', { name: "New member's name" }).fill('Guest');
  await page.getByRole('button', { name: 'Add' }).click();
  await expect(page.getByTestId('member')).toHaveCount(3);
  await expect(serving).toHaveText('2.4');

  await page.reload();
  await expect(serving).toHaveText('2.4');
});

test('formats factors with a decimal comma in Polish', async ({ page }) => {
  await mockHousehold(page, 'pl');

  await page.goto('/household/members');

  await expect(page.getByRole('heading', { level: 2, name: 'Domownicy' })).toBeVisible();
  await expect(page.getByTestId('household-serving')).toHaveText('1,3');
  await expect(page.getByTestId('portion-factor').nth(1)).toHaveText('0,3');
  await expect(page.getByText('Mnożnik porcji od 0,05 do 2, co 0,05.')).toBeVisible();
});
