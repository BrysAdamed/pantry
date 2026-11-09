import { Browser, expect, Page, test } from '@playwright/test';

const creationOptions = {
  rp: { id: 'localhost', name: 'localhost' },
  user: { id: 'dXNlci0x', name: 'Alex', displayName: 'Alex' },
  challenge: 'Y2hhbGxlbmdlLWNoYWxsZW5nZS1jaGFsbGVuZ2U',
  pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
  timeout: 60000,
  authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
  attestation: 'none',
};

interface Passkey {
  id: string;
  name: string | null;
  createdAt: string;
  lastUsedAt: string | null;
}

/** The API of one signed-in user, shared by every device (page) that uses it. */
function account() {
  return {
    me: {
      id: '1',
      displayName: 'Alex',
      role: 'admin',
      userName: null as string | null,
      uiLanguage: 'en' as string | null,
      theme: 'system',
      signInMethods: ['passkey'],
    },
    passkeys: [{ id: 'cGhvbmU', name: 'Phone', createdAt: '2026-11-08T10:00:00Z', lastUsedAt: '2026-11-09T08:00:00Z' }] as Passkey[],
    signedOutOthers: 0,
    password: null as string | null,
  };
}

type Account = ReturnType<typeof account>;

async function mockAccount(page: Page, state: Account, deviceLanguage = 'en') {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) => route.fulfill({ json: state.me }));
  await page.route(/\/api\/me\/(ui-language|theme)$/, (route) => {
    Object.assign(state.me, route.request().postDataJSON());
    return route.fulfill({ status: 204 });
  });
  await page.route('/api/me/sign-out-others', (route) => {
    state.signedOutOthers++;
    return route.fulfill({ status: 204 });
  });
  await page.route('/api/me/password', (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      state.me.userName ??= request.postDataJSON().userName;
      state.password = request.postDataJSON().password;
    } else if (request.method() === 'PUT') {
      if (request.postDataJSON().currentPassword !== state.password) {
        return route.fulfill({ status: 400, json: { errors: { CurrentPassword: ['The current password is wrong.'] } } });
      }
      state.password = request.postDataJSON().newPassword;
    } else {
      state.password = null;
    }
    state.signedOutOthers++;
    state.me.signInMethods = state.password ? ['passkey', 'password'] : ['passkey'];
    return route.fulfill({ json: state.me });
  });
  await page.route('/api/me/passkeys**', (route) => {
    const request = route.request();
    const [, , , , id, field] = new URL(request.url()).pathname.split('/');
    if (request.method() === 'GET') {
      return route.fulfill({ json: state.passkeys });
    }
    if (request.method() === 'POST') {
      const passkey: Passkey = { id: `key${state.passkeys.length}`, name: null, createdAt: '2026-11-09T12:00:00Z', lastUsedAt: null };
      state.passkeys.push(passkey);
      return route.fulfill({ status: 201, json: passkey });
    }
    if (request.method() === 'PUT' && field === 'name') {
      state.passkeys.find((p) => p.id === id)!.name = request.postDataJSON().name;
      return route.fulfill({ status: 204 });
    }
    if (state.passkeys.length === 1) {
      return route.fulfill({ status: 409, json: { title: 'The last passkey cannot be removed.' } });
    }
    state.passkeys = state.passkeys.filter((p) => p.id !== id);
    return route.fulfill({ status: 204 });
  });
  await page.route('/api/me/passkeys/options', (route) => route.fulfill({ json: creationOptions }));
  await page.addInitScript((lang) => {
    if (!sessionStorage.getItem('e2e.seeded')) {
      localStorage.setItem('pantry.uiLanguage', lang);
      sessionStorage.setItem('e2e.seeded', '1');
    }
  }, deviceLanguage);
}

/** A Chrome DevTools virtual authenticator, so the real WebAuthn API creates the passkey. */
async function addVirtualAuthenticator(page: Page) {
  const session = await page.context().newCDPSession(page);
  await session.send('WebAuthn.enable');
  await session.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      transport: 'internal',
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });
}

async function otherDevice(browser: Browser, state: Account, deviceLanguage: string): Promise<Page> {
  const context = await browser.newContext();
  const page = await context.newPage();
  await mockAccount(page, state, deviceLanguage);
  return page;
}

test('a UI language chosen on one device applies on another after reload', async ({ page, browser }) => {
  const state = account();
  await mockAccount(page, state);
  const laptop = await otherDevice(browser, state, 'en');
  await laptop.goto('/planner');
  await expect(laptop.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Plan this week');

  await page.goto('/household/account');
  await page.getByTestId('account-language').getByTestId('language-pl').click();
  await expect(page.getByRole('heading', { level: 2, name: 'Moje konto' })).toBeVisible();
  await page.getByTestId('account-theme').getByTestId('theme-dark').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  await laptop.reload();
  await expect(laptop.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Zaplanuj ten tydzień');
  await expect(laptop.locator('html')).toHaveAttribute('data-theme', 'dark');
  await laptop.context().close();
});

test('adds, renames and removes a passkey, and keeps the last one', async ({ page }) => {
  const state = account();
  await mockAccount(page, state);
  await addVirtualAuthenticator(page);
  await page.goto('/household/account');
  const passkeys = page.getByTestId('passkey');
  await expect(passkeys).toHaveCount(1);
  await expect(page.getByTestId('passkey-remove')).toBeDisabled();

  await page.getByTestId('passkey-add').click();
  await expect(passkeys).toHaveCount(2);
  await expect(passkeys.nth(1)).toContainText('not used to sign in yet');

  await page.getByRole('button', { name: 'Rename Passkey' }).click();
  await page.getByRole('textbox', { name: 'Passkey name' }).fill('Laptop');
  await page.keyboard.press('Enter');
  await expect(passkeys.nth(1)).toContainText('Laptop');
  expect(state.passkeys[1].name).toBe('Laptop');

  await page.getByRole('button', { name: 'Remove Phone' }).click();
  await expect(page.getByText('Remove Phone? Other devices will have to sign in again.')).toBeVisible();
  await page.getByTestId('passkey-remove-confirm').click();
  await expect(passkeys).toHaveCount(1);
  await expect(passkeys).toContainText('Laptop');
  await expect(page.getByTestId('passkey-only-one')).toBeVisible();
});

test('signs out other devices', async ({ page }) => {
  const state = account();
  await mockAccount(page, state);
  await page.goto('/household/account');

  await page.getByTestId('sign-out-others').click();

  await expect(page.getByRole('status').filter({ hasText: 'Other devices are signed out.' })).toBeVisible();
  expect(state.signedOutOthers).toBe(1);
});

test('opens My account from the avatar menu', async ({ page }) => {
  await mockAccount(page, account());
  await page.route('/api/household/members', (route) => route.fulfill({ json: { members: [], householdServing: 0 } }));
  await page.goto('/planner');

  await page.getByTestId('avatar-menu-trigger').filter({ visible: true }).click();
  await page.getByTestId('my-account').click();

  await expect(page).toHaveURL('/household/account');
  await expect(page.getByRole('heading', { level: 2, name: 'My account' })).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('sets, changes and removes the password, signing out other devices each time', async ({ page }) => {
  const state = account();
  await mockAccount(page, state);
  await page.goto('/household/account');
  const section = page.getByTestId('account-password');
  await expect(section.getByTestId('account-user-name')).toHaveText('none yet');

  await section.getByTestId('password-set').click();
  await section.getByLabel('Username').fill('alex');
  await section.getByLabel('Password', { exact: true }).fill('pantry shelf 2026');
  await section.getByLabel('Repeat the password').fill('pantry shelf 2026');
  await section.getByTestId('password-save').click();
  await expect(section.getByRole('status')).toHaveText('Password saved. Other devices are signed out.');
  await expect(section.getByTestId('account-user-name')).toHaveText('alex');
  expect(state.password).toBe('pantry shelf 2026');

  await section.getByTestId('password-change').click();
  await expect(section.getByLabel('Username')).toHaveValue('alex');
  await section.getByLabel('Current password').fill('not the password');
  await section.getByLabel('Password', { exact: true }).fill('a brand new password');
  await section.getByLabel('Repeat the password').fill('a brand new password');
  await section.getByTestId('password-save').click();
  await expect(section.getByRole('alert')).toHaveText('The current password is wrong.');
  await section.getByLabel('Current password').fill('pantry shelf 2026');
  await section.getByTestId('password-save').click();
  await expect(section.getByRole('status')).toBeVisible();
  expect(state.password).toBe('a brand new password');

  await section.getByTestId('password-remove').click();
  await section.getByTestId('password-remove-confirm').click();
  await expect(section.getByRole('status')).toHaveText('Password removed. Other devices are signed out.');
  await expect(section.getByTestId('password-set')).toBeVisible();
  expect(state.password).toBeNull();
  expect(state.signedOutOthers).toBe(3);
});

test('lets the last passkey go once a password is set', async ({ page }) => {
  const state = account();
  state.me.userName = 'alex';
  state.me.signInMethods = ['passkey', 'password'];
  state.password = 'pantry shelf 2026';
  await mockAccount(page, state);
  await page.goto('/household/account');

  await expect(page.getByTestId('passkey-remove')).toBeEnabled();
  await expect(page.getByTestId('passkey-only-one')).toHaveCount(0);
});
