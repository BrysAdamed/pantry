import { generateKeyPairSync } from 'node:crypto';
import { expect, Page, test } from '@playwright/test';

const user = { id: '1', displayName: 'Alex', role: 'user', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };

const requestOptions = {
  challenge: 'Y2hhbGxlbmdlLWNoYWxsZW5nZS1jaGFsbGVuZ2U',
  rpId: 'localhost',
  timeout: 60000,
  userVerification: 'required',
};

interface MockApi {
  signedIn: boolean;
  signInStatus: number;
  passwords: Record<string, string>;
}

/** A household with a session that starts signed out; sign-in and sign-out flip it like the real API. */
async function mockApi(page: Page, overrides: Partial<MockApi> = {}): Promise<MockApi> {
  const api: MockApi = { signedIn: false, signInStatus: 200, passwords: { alex: 'pantry shelf 2026' }, ...overrides };
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) =>
    api.signedIn ? route.fulfill({ json: user }) : route.fulfill({ status: 401 }),
  );
  await page.route('/api/antiforgery', (route) => route.fulfill({ status: 204 }));
  await page.route('/api/auth/passkey-options', (route) => route.fulfill({ json: requestOptions }));
  await page.route('/api/auth/sign-in', (route) => {
    if (api.signInStatus !== 200) {
      return route.fulfill({ status: api.signInStatus, json: { title: 'Passkey sign-in failed.' } });
    }
    api.signedIn = true;
    return route.fulfill({ json: user });
  });
  await page.route('/api/auth/sign-in/password', (route) => {
    const { userName, password } = route.request().postDataJSON();
    if (api.passwords[userName.toLowerCase()] !== password) {
      return route.fulfill({ status: 401, json: { title: 'Password sign-in failed.' } });
    }
    api.signedIn = true;
    return route.fulfill({ json: { ...user, userName, signInMethods: ['password'] } });
  });
  await page.route('/api/auth/sign-out', (route) => {
    api.signedIn = false;
    return route.fulfill({ status: 204 });
  });
  await page.addInitScript(() => {
    if (!sessionStorage.getItem('e2e.seeded')) {
      localStorage.setItem('pantry.uiLanguage', 'en');
      sessionStorage.setItem('e2e.seeded', '1');
    }
  });
  return api;
}

/** Without autofill, so each test drives the modal passkey window itself. */
async function withoutPasskeyAutofill(page: Page) {
  await page.addInitScript(() => {
    PublicKeyCredential.isConditionalMediationAvailable = () => Promise.resolve(false);
  });
}

/** A Chrome DevTools virtual authenticator holding one discoverable passkey for localhost. */
async function addPasskey(page: Page) {
  const session = await page.context().newCDPSession(page);
  await session.send('WebAuthn.enable');
  const { authenticatorId } = await session.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      transport: 'internal',
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });
  const { privateKey } = generateKeyPairSync('ec', { namedCurve: 'P-256' });
  await session.send('WebAuthn.addCredential', {
    authenticatorId,
    credential: {
      credentialId: Buffer.from('e2e-passkey').toString('base64'),
      isResidentCredential: true,
      rpId: 'localhost',
      privateKey: privateKey.export({ type: 'pkcs8', format: 'der' }).toString('base64'),
      userHandle: Buffer.from('user-1').toString('base64'),
      signCount: 0,
    },
  });
}

async function closePasskeyWindow(page: Page) {
  await page.addInitScript(() => {
    navigator.credentials.get = () => Promise.reject(new DOMException('closed', 'NotAllowedError'));
  });
}

test('sends a signed-out user to sign-in and back to the planner afterwards', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  await addPasskey(page);
  const signedIn = page.waitForRequest('/api/auth/sign-in');

  await page.goto('/planner');
  await expect(page).toHaveURL('/sign-in?returnUrl=%2Fplanner');
  await page.getByTestId('sign-in-passkey').click();

  const body = (await signedIn).postDataJSON();
  expect(body.credential.type).toBe('public-key');
  expect(body.credential.response.signature).toBeTruthy();
  await expect(page).toHaveURL('/planner');
  await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Plan this week');
});

test('offers to try again when the passkey window is closed', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  await closePasskeyWindow(page);
  await page.goto('/sign-in');

  await page.getByTestId('sign-in-passkey').click();

  await expect(page.getByRole('alert')).toContainText('Sign-in interrupted.');
  await expect(page.getByTestId('sign-in-passkey')).toHaveText('Try again');
  await expect(page.getByTestId('sign-in-passkey')).toBeEnabled();
});

test('tells when the passkey is not known here', async ({ page }) => {
  await mockApi(page, { signInStatus: 401 });
  await withoutPasskeyAutofill(page);
  await addPasskey(page);
  await page.goto('/sign-in');

  await page.getByTestId('sign-in-passkey').click();

  await expect(page.getByRole('alert')).toContainText('This passkey is not known here.');
  await expect(page).toHaveURL('/sign-in');
});

test('offers to try again when the server cannot sign in', async ({ page }) => {
  await mockApi(page, { signInStatus: 500 });
  await withoutPasskeyAutofill(page);
  await addPasskey(page);
  await page.goto('/sign-in');

  await page.getByTestId('sign-in-passkey').click();

  await expect(page.getByRole('alert')).toContainText('Could not sign in.');
  await expect(page.getByTestId('sign-in-passkey')).toHaveText('Try again');
  await expect(page.getByTestId('sign-in-passkey')).toBeEnabled();
});

test('explains that passkeys do not work in a browser without them', async ({ page }) => {
  await mockApi(page);
  await page.addInitScript(() => {
    Object.defineProperty(window, 'PublicKeyCredential', { value: undefined, configurable: true });
  });

  await page.goto('/sign-in');

  await expect(page.getByRole('alert')).toContainText('Passkeys do not work here.');
  await expect(page.getByTestId('sign-in-passkey')).toBeDisabled();
});

test('signs in with a passkey picked from the autofill where the browser supports it', async ({ page }) => {
  await mockApi(page);
  await page.addInitScript(() => {
    PublicKeyCredential.isConditionalMediationAvailable = () => Promise.resolve(true);
  });
  await addPasskey(page);
  await page.goto('/sign-in');

  await expect(page.getByTestId('sign-in-user-name')).toHaveAttribute('autocomplete', 'username webauthn');

  await expect(page).toHaveURL('/planner');
});

test('shows sign-in in Polish', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  await page.addInitScript(() => localStorage.setItem('pantry.uiLanguage', 'pl'));

  await page.goto('/sign-in');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Spiżarnia');
  await expect(page.getByTestId('sign-in-passkey')).toHaveText('Zaloguj się kluczem dostępu');
});

test('keeps a signed-in user away from the sign-in screen', async ({ page }) => {
  await mockApi(page, { signedIn: true });

  await page.goto('/sign-in?returnUrl=%2Frecipes');

  await expect(page).toHaveURL('/recipes');
});

test('fails closed when the API cannot be reached', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  await page.route('/api/me', (route) => route.abort('connectionrefused'));

  await page.goto('/shopping');

  await expect(page).toHaveURL('/sign-in?returnUrl=%2Fshopping');
  await expect(page.getByRole('navigation')).toHaveCount(0);
});

test('signs out and the back button does not show the app again', async ({ page }) => {
  await mockApi(page, { signedIn: true });
  await withoutPasskeyAutofill(page);
  await page.goto('/planner');
  await page.getByRole('link', { name: 'Recipes' }).filter({ visible: true }).click();
  await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Add your first recipe');

  await page.getByTestId('avatar-menu-trigger').filter({ visible: true }).click();
  await page.getByTestId('sign-out').click();

  await expect(page).toHaveURL('/sign-in');
  await page.goBack();
  await expect(page).toHaveURL(/\/sign-in\?returnUrl=/);
  await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveCount(0);
  await expect(page.getByRole('navigation')).toHaveCount(0);
});

test('signs in with a username and password and goes back to the requested page', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  const signedIn = page.waitForRequest('/api/auth/sign-in/password');

  await page.goto('/recipes');
  await expect(page).toHaveURL('/sign-in?returnUrl=%2Frecipes');
  await page.getByLabel('Username').fill('alex');
  await page.getByLabel('Password').fill('pantry shelf 2026');
  await page.getByLabel('Password').press('Enter');

  expect((await signedIn).postDataJSON()).toEqual({ userName: 'alex', password: 'pantry shelf 2026' });
  await expect(page).toHaveURL('/recipes');
  await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveText('Add your first recipe');
});

test('gives the same message for a wrong password and an unknown username', async ({ page }) => {
  await mockApi(page);
  await withoutPasskeyAutofill(page);
  await page.goto('/sign-in');

  await page.getByLabel('Username').fill('alex');
  await page.getByLabel('Password').fill('not the password');
  await page.getByTestId('sign-in-with-password').click();
  await expect(page.getByRole('alert')).toContainText('Wrong username or password.');
  const wrongPassword = await page.getByRole('alert').textContent();

  await page.getByLabel('Username').fill('nobody');
  const unknownUser = page.waitForResponse('/api/auth/sign-in/password');
  await page.getByTestId('sign-in-with-password').click();

  expect((await unknownUser).status()).toBe(401);
  await expect(page.getByRole('alert')).toHaveText(wrongPassword!);
  await expect(page).toHaveURL('/sign-in');
});

test('signs in with a password where the browser has no passkeys', async ({ page }) => {
  await mockApi(page);
  await page.addInitScript(() => {
    Object.defineProperty(window, 'PublicKeyCredential', { value: undefined, configurable: true });
  });
  await page.goto('/sign-in');

  await page.getByLabel('Username').fill('alex');
  await page.getByLabel('Password').fill('pantry shelf 2026');
  await page.getByTestId('sign-in-with-password').click();

  await expect(page).toHaveURL('/planner');
});
