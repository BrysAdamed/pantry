import { expect, Page, test } from '@playwright/test';

interface Member {
  id: string;
  name: string;
  portionFactor: number;
  position: number;
  account: 'admin' | 'user' | null;
  userId: string | null;
  signInMethods: ('passkey' | 'password')[] | null;
}

interface MealSlot {
  id: string;
  name: string;
  position: number;
  isEnabled: boolean;
}

interface Dislike {
  id: string;
  ingredientId: string;
  ingredientName: string;
  kind: 'never' | 'avoid';
  reason: string | null;
}

interface PlanningRule {
  id: string;
  text: string;
  position: number;
  isEnabled: boolean;
}

const sections = ['Members', 'Meals', "We don't eat", 'Planning rules', 'Content language', 'Access tokens', 'My account'];

async function mockHousehold(page: Page, language: 'en' | 'pl' = 'en', role: 'admin' | 'user' = 'admin') {
  const members: Member[] = [
    { id: 'a', name: 'Alex', portionFactor: 1, position: 0, account: 'admin', userId: '1', signInMethods: ['passkey'] },
    { id: 't', name: 'Toddler', portionFactor: 0.3, position: 1, account: null, userId: null, signInMethods: null },
  ];
  const links: object[] = [];
  await page.route('/api/passkey-links', (route) =>
    role === 'admin' ? route.fulfill({ json: links }) : route.fulfill({ status: 403 }),
  );
  await page.route('/api/passkey-links/invites', (route) => {
    const memberId = route.request().postDataJSON().memberId;
    const link = { id: 'l1', kind: 'invite', memberId, userId: null, createdAt: '2026-11-09T10:00:00Z', expiresAt: '2026-11-11T10:00:00Z' };
    links.splice(0, links.length, link);
    return route.fulfill({ status: 201, json: { ...link, token: 'e2e-token' } });
  });
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    route.fulfill({ json: { id: '1', displayName: 'Alex', role, userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] } }),
  );
  await page.route('/api/household/members**', async (route) => {
    const request = route.request();
    const id = new URL(request.url()).pathname.split('/')[4];
    if (request.method() === 'GET') {
      return route.fulfill({ json: { members, householdServing: members.reduce((s, m) => s + m.portionFactor, 0) } });
    }
    if (request.method() === 'POST') {
      const member: Member = { id: `m${members.length}`, position: members.length, account: null, userId: null, signInMethods: null, ...request.postDataJSON() };
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
  const slots: MealSlot[] = ['Breakfast', 'Second breakfast', 'Lunch', 'Snack', 'Dinner'].map((name, position) => ({
    id: `s${position}`,
    name,
    position,
    isEnabled: position !== 1,
  }));
  await page.route('/api/household/meal-slots**', async (route) => {
    const request = route.request();
    const [, , , , id, field] = new URL(request.url()).pathname.split('/');
    if (request.method() === 'GET') {
      return route.fulfill({ json: [...slots].sort((a, b) => a.position - b.position) });
    }
    if (request.method() === 'POST') {
      const slot: MealSlot = { id: `s${slots.length}`, position: slots.length, isEnabled: true, ...request.postDataJSON() };
      slots.push(slot);
      return route.fulfill({ status: 201, json: slot });
    }
    if (id === 'order') {
      (request.postDataJSON().mealSlotIds as string[]).forEach((slotId, position) => {
        slots.find((s) => s.id === slotId)!.position = position;
      });
      return route.fulfill({ status: 204 });
    }
    const slot = slots.find((s) => s.id === id)!;
    Object.assign(slot, field === 'name' ? request.postDataJSON() : { isEnabled: request.postDataJSON().isEnabled });
    return route.fulfill({ json: slot });
  });
  const rules: PlanningRule[] = [
    { id: 'r0', text: 'Weekday dinners under 30 minutes', position: 0, isEnabled: true },
    { id: 'r1', text: 'No deep frying', position: 1, isEnabled: false },
  ];
  await page.route('/api/household/planning-rules**', async (route) => {
    const request = route.request();
    const [, , , , id, field] = new URL(request.url()).pathname.split('/');
    if (request.method() === 'GET') {
      return route.fulfill({ json: [...rules].sort((a, b) => a.position - b.position) });
    }
    if (request.method() === 'POST') {
      const rule: PlanningRule = { id: `r${rules.length}`, position: rules.length, isEnabled: true, ...request.postDataJSON() };
      rules.push(rule);
      return route.fulfill({ status: 201, json: rule });
    }
    if (id === 'order') {
      (request.postDataJSON().planningRuleIds as string[]).forEach((ruleId, position) => {
        rules.find((r) => r.id === ruleId)!.position = position;
      });
      return route.fulfill({ status: 204 });
    }
    const index = rules.findIndex((r) => r.id === id);
    if (request.method() === 'DELETE') {
      rules.splice(index, 1);
      rules.sort((a, b) => a.position - b.position).forEach((r, position) => (r.position = position));
      return route.fulfill({ status: 204 });
    }
    Object.assign(rules[index], field === 'text' ? request.postDataJSON() : { isEnabled: request.postDataJSON().isEnabled });
    return route.fulfill({ json: rules[index] });
  });
  const catalog = [
    { id: 'i-walnuts', name: 'Walnuts' },
    { id: 'i-coriander', name: 'Coriander' },
    { id: 'i-peanuts', name: 'Peanuts' },
  ].map((i) => ({ ...i, aliases: [], baseUnit: 'g', category: 'other', isStaple: false, needsReview: false, conversions: [] }));
  await page.route(/\/api\/catalog\/ingredients\?.*$/, (route) => {
    const search = (new URL(route.request().url()).searchParams.get('search') ?? '').toLowerCase();
    const items = catalog.filter((i) => i.name.toLowerCase().includes(search));
    return route.fulfill({ json: { items, total: items.length, page: 1, pageSize: 8 } });
  });
  await page.route('/api/catalog/ingredients/resolve', (route) => {
    const ingredient = { ...catalog[0], id: `i-${catalog.length}`, name: route.request().postDataJSON().name, needsReview: true };
    catalog.push(ingredient);
    return route.fulfill({ json: ingredient });
  });
  const dislikes: Dislike[] = [
    { id: 'd0', ingredientId: 'i-walnuts', ingredientName: 'Walnuts', kind: 'never', reason: 'allergy' },
    { id: 'd1', ingredientId: 'i-coriander', ingredientName: 'Coriander', kind: 'avoid', reason: null },
  ];
  await page.route('/api/household/dislikes**', async (route) => {
    const request = route.request();
    const id = new URL(request.url()).pathname.split('/')[4];
    if (request.method() === 'GET') {
      return route.fulfill({ json: [...dislikes].sort((a, b) => a.kind.localeCompare(b.kind) * -1) });
    }
    if (request.method() === 'POST') {
      const { ingredientId, kind, reason } = request.postDataJSON();
      const name = catalog.find((i) => i.id === ingredientId)!.name;
      const dislike: Dislike = { id: `d${dislikes.length}`, ingredientId, ingredientName: name, kind, reason };
      dislikes.push(dislike);
      return route.fulfill({ status: 201, json: dislike });
    }
    const index = dislikes.findIndex((d) => d.id === id);
    if (request.method() === 'DELETE') {
      dislikes.splice(index, 1);
      return route.fulfill({ status: 204 });
    }
    Object.assign(dislikes[index], request.postDataJSON());
    return route.fulfill({ json: dislikes[index] });
  });
  const contentLanguage: { contentLanguage: string } = { contentLanguage: language };
  await page.route('/api/household/content-language', async (route) => {
    if (route.request().method() === 'PUT') {
      contentLanguage.contentLanguage = route.request().postDataJSON().contentLanguage;
    }
    return route.fulfill({ json: contentLanguage });
  });
  const cooldown = { cooldownDays: 21 };
  await page.route('/api/household/cooldown', async (route) => {
    if (route.request().method() === 'PUT') {
      cooldown.cooldownDays = route.request().postDataJSON().cooldownDays;
    }
    return route.fulfill({ json: cooldown });
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

  await list.getByRole('link', { name: /^We don't eat/ }).click();
  await expect(page).toHaveURL('/household/dislikes');
  await expect(page.getByRole('heading', { level: 2, name: "We don't eat" })).toBeVisible();
  await expect(page.getByRole('heading', { level: 3, name: 'Never' })).toBeVisible();
  await expect(list).toBeVisible();
});

test('adds, moves and removes dislikes in the never and avoid columns', async ({ page }) => {
  await mockHousehold(page);
  await page.goto('/household/dislikes');
  const never = page.getByTestId('dislikes-never');
  const avoid = page.getByTestId('dislikes-avoid');
  await expect(never.getByTestId('dislike')).toHaveText(['Walnuts · allergy']);
  await expect(avoid.getByTestId('dislike')).toHaveText(['Coriander']);

  await never.getByRole('combobox', { name: 'Add to "never"' }).fill('pea');
  await never.getByRole('option', { name: /Peanuts/ }).click();
  await expect(never.getByTestId('dislike-chosen')).toHaveText('Peanuts');
  await never.getByRole('textbox', { name: 'Reason' }).fill('allergy');
  await never.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(never.getByTestId('dislike')).toHaveText(['Walnuts · allergy', 'Peanuts · allergy']);

  await avoid.getByRole('combobox', { name: 'Add to "avoid"' }).fill('Okra');
  await avoid.getByTestId('new-option').click();
  await expect(avoid.getByTestId('dislike-chosen')).toContainText('new');
  await avoid.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(avoid.getByTestId('dislike')).toHaveText(['Coriander', 'Okra']);

  await never.getByRole('button', { name: 'Move Walnuts to "avoid"' }).click();
  await expect(avoid.getByTestId('dislike')).toHaveText(['Coriander', 'Okra', 'Walnuts']);
  await avoid.getByRole('button', { name: 'Remove Coriander' }).click();
  await expect(avoid.getByTestId('dislike')).toHaveText(['Okra', 'Walnuts']);

  await page.reload();
  await expect(never.getByTestId('dislike')).toHaveText(['Peanuts · allergy']);
  await expect(avoid.getByTestId('dislike')).toHaveCount(2);
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

test('lets the admin invite a member and copy the link', async ({ page, context, baseURL }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await mockHousehold(page);
  await page.goto('/household/members');
  const toddler = page.getByRole('group', { name: 'Toddler' });

  await toddler.getByRole('button', { name: 'Invite' }).click();

  const panel = page.getByTestId('issued-link');
  await expect(panel.getByRole('heading')).toHaveText('Invite for Toddler');
  await expect(panel.getByRole('textbox')).toHaveValue(`${baseURL}/link/e2e-token`);
  await panel.getByRole('button', { name: 'Copy' }).click();
  await expect(panel.getByRole('button', { name: 'Copied' })).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(`${baseURL}/link/e2e-token`);
  await expect(page.getByText('invite sent', { exact: true })).toBeVisible();
  await expect(toddler.getByRole('button', { name: 'New link' })).toBeVisible();

  await panel.getByRole('button', { name: 'Done' }).click();
  await expect(panel).toHaveCount(0);
});

test('hides account administration from users who are not admins', async ({ page }) => {
  await mockHousehold(page, 'en', 'user');

  await page.goto('/household/members');

  await expect(page.getByTestId('member')).toHaveCount(2);
  await expect(page.getByRole('button', { name: 'Invite' })).toHaveCount(0);
});

test('reorders, renames, switches off and adds meals', async ({ page }) => {
  await mockHousehold(page);
  await page.goto('/household/slots');
  const names = page.getByTestId('meal-slot').getByRole('textbox');
  await expect(names).toHaveCount(5);
  await expect(page.getByTestId('meal-slots-note')).toHaveText('Meals a day in the plan: 4.');

  await page.getByRole('button', { name: 'Move Dinner up', exact: true }).click();
  await expect(page.getByTestId('meal-slot').nth(3).getByRole('textbox')).toHaveValue('Dinner');

  const lunch = page.getByRole('textbox', { name: 'Name of meal 3' });
  await lunch.fill('Main meal');
  await lunch.press('Enter');
  await page.getByRole('switch', { name: 'Snack in the plan', exact: true }).click();
  await expect(page.getByTestId('meal-slots-note')).toHaveText('Meals a day in the plan: 3.');

  await page.getByRole('textbox', { name: 'New meal', exact: true }).fill('Dinner out');
  await page.getByRole('button', { name: 'Add meal', exact: true }).click();
  await expect(names).toHaveCount(6);

  await page.reload();
  await expect
    .poll(() => names.evaluateAll((inputs) => inputs.map((input) => (input as HTMLInputElement).value)))
    .toEqual(['Breakfast', 'Second breakfast', 'Main meal', 'Dinner', 'Snack', 'Dinner out']);
  await expect(page.getByRole('switch', { name: 'Snack in the plan', exact: true })).toHaveAttribute('aria-checked', 'false');
});

test('keeps the last meal in the plan', async ({ page }) => {
  await mockHousehold(page, 'pl');
  await page.goto('/household/slots');
  await expect(page.getByRole('heading', { level: 2, name: 'Posiłki' })).toBeVisible();

  for (const name of ['Breakfast', 'Lunch', 'Snack']) {
    await page.getByRole('switch', { name: `${name} w planie`, exact: true }).click();
  }

  await expect(page.getByRole('switch', { name: 'Dinner w planie' })).toBeDisabled();
  await expect(page.getByTestId('meal-slots-note')).toContainText('Co najmniej jeden posiłek zostaje w planie.');
});

test('adds, switches off, reorders and removes planning rules', async ({ page }) => {
  await mockHousehold(page);
  await page.goto('/household/rules');
  const texts = page.getByTestId('planning-rule').getByRole('textbox');
  await expect(texts).toHaveCount(2);
  await expect(page.getByTestId('planning-rules-note')).toHaveText('Rules the agent reads: 1.');

  await page.getByRole('textbox', { name: 'New rule', exact: true }).fill('Fish at least once a week');
  await page.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(texts).toHaveCount(3);
  await page.getByRole('switch', { name: 'Rule 1 on', exact: true }).click();
  await expect(page.getByTestId('planning-rules-note')).toHaveText('Rules the agent reads: 1.');
  await page.getByRole('button', { name: 'Move rule 3 up', exact: true }).click();
  await expect(texts.nth(1)).toHaveValue('Fish at least once a week');

  await page.getByRole('button', { name: 'Remove rule 3', exact: true }).click();
  await page.getByRole('button', { name: 'Remove', exact: true }).click();
  await expect(texts).toHaveCount(2);

  await page.reload();
  await expect
    .poll(() => texts.evaluateAll((inputs) => inputs.map((input) => (input as HTMLTextAreaElement).value)))
    .toEqual(['Weekday dinners under 30 minutes', 'Fish at least once a week']);
  await expect(page.getByRole('switch', { name: 'Rule 1 on', exact: true })).toHaveAttribute('aria-checked', 'false');
});

test('changes the household break before a repeat', async ({ page }) => {
  await mockHousehold(page, 'pl');
  await page.goto('/household/rules');
  const days = page.getByTestId('cooldown').getByRole('spinbutton');
  await expect(days).toHaveValue('21');

  await days.fill('400');
  await days.press('Enter');
  await expect(page.getByTestId('cooldown').getByRole('alert')).toHaveText('Wpisz pełną liczbę dni od 0 do 365.');

  await days.fill('14');
  await days.press('Enter');
  await expect(page.getByTestId('cooldown').getByRole('alert')).toHaveCount(0);

  await page.reload();
  await expect(page.getByTestId('cooldown').getByRole('spinbutton')).toHaveValue('14');
});

test('picks the content language independently of the interface language', async ({ page }) => {
  await mockHousehold(page, 'pl');
  await page.goto('/household/language');
  await expect(page.getByRole('heading', { level: 2, name: 'Język treści' })).toBeVisible();
  await expect(page.getByRole('radio', { name: /Polski/ })).toBeChecked();

  await page.getByRole('radio', { name: /Deutsch/ }).check();
  await expect(page.getByRole('radio', { name: /Deutsch/ })).toBeChecked();

  await page.reload();
  await expect(page.getByRole('radio', { name: /Deutsch/ })).toBeChecked();
  await expect(page.getByRole('heading', { level: 2, name: 'Język treści' })).toBeVisible();
});