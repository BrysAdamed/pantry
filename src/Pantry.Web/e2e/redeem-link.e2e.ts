import { expect, Page, test } from '@playwright/test';

const creationOptions = {
  rp: { id: 'localhost', name: 'localhost' },
  user: { id: 'dXNlci0y', name: 'Sam', displayName: 'Sam' },
  challenge: 'Y2hhbGxlbmdlLWNoYWxsZW5nZS1jaGFsbGVuZ2U',
  pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
  timeout: 60000,
  authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
  attestation: 'none',
};

const user = { id: '2', displayName: 'Sam', role: 'user', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };
const expiresAt = '2026-11-11T10:00:00Z';

type LinkAnswer = { status: 200; kind: 'invite' | 'recovery'; userName?: string } | { status: 410; state: 'expired' | 'used' | 'revoked' } | { status: 404 };

/** The redeem API for the token `tok`; the session starts signed out and the completed link signs the user in. */
async function mockLink(page: Page, answer: LinkAnswer, language: 'en' | 'pl' = 'en') {
  let signedIn = false;
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));
  await page.route('/api/me', (route) => (signedIn ? route.fulfill({ json: user }) : route.fulfill({ status: 401 })));
  await page.route('/api/antiforgery', (route) => route.fulfill({ status: 204 }));
  await page.route('/api/passkey-links/redeem/tok', (route) => {
    if (answer.status === 200) {
      return route.fulfill({ json: { kind: answer.kind, name: 'Sam', userName: answer.userName ?? null, invitedBy: 'Alex', expiresAt } });
    }
    return route.fulfill({ status: answer.status, json: answer.status === 410 ? { state: answer.state } : {} });
  });
  await page.route('/api/passkey-links/redeem/tok/passkey-options', (route) => route.fulfill({ json: creationOptions }));
  await page.route('/api/passkey-links/redeem/tok/complete-with-password', (route) => {
    signedIn = true;
    return route.fulfill({ json: { ...user, signInMethods: ['password'] } });
  });
  await page.route('/api/passkey-links/redeem/tok/complete', (route) => {
    signedIn = true;
    return route.fulfill({ json: user });
  });
  await page.addInitScript((lang) => localStorage.setItem('pantry.uiLanguage', lang), language);
}

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

test('accepts an invite: confirms the name, creates the passkey and opens the app', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'invite' });
  await addVirtualAuthenticator(page);
  const options = page.waitForRequest('/api/passkey-links/redeem/tok/passkey-options');
  const completed = page.waitForRequest('/api/passkey-links/redeem/tok/complete');
  await page.goto('/link/tok');

  await expect(page.getByTestId('redeem-title')).toHaveText('Alex invites you to Pantry');
  await expect(page.getByRole('textbox')).toHaveValue('Sam');
  await page.getByRole('textbox').fill('Sam B');
  await page.getByTestId('redeem-create').click();

  expect((await options).postDataJSON()).toEqual({ displayName: 'Sam B' });
  expect((await completed).postDataJSON().credential.response.attestationObject).toBeTruthy();
  await expect(page.getByTestId('redeem-done')).toHaveText('Welcome home!');
  await page.getByTestId('redeem-open-app').click();
  await expect(page).toHaveURL('/planner');
});

test('opens a recovery link without asking for a name', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'recovery' });
  await addVirtualAuthenticator(page);
  await page.goto('/link/tok');

  await expect(page.getByTestId('redeem-title')).toHaveText('Get back into Pantry, Sam');
  await expect(page.getByRole('textbox')).toHaveCount(0);
  await page.getByTestId('redeem-create').click();

  await expect(page.getByTestId('redeem-done')).toBeVisible();
});

test('offers to try again when the passkey window is closed', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'invite' });
  await page.addInitScript(() => {
    navigator.credentials.create = () => Promise.reject(new DOMException('closed', 'NotAllowedError'));
  });
  await page.goto('/link/tok');

  await page.getByTestId('redeem-create').click();

  await expect(page.getByRole('alert')).toContainText('Passkey creation cancelled.');
  await expect(page.getByTestId('redeem-create')).toHaveText('Try again');
});

for (const [state, title] of [
  ['expired', 'This link has expired'],
  ['used', 'This link was already used'],
  ['revoked', 'This link was cancelled'],
] as const) {
  test(`rejects a ${state} link with a clear screen`, async ({ page }) => {
    await mockLink(page, { status: 410, state });

    await page.goto('/link/tok');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(title);
    await expect(page.getByTestId('redeem-create')).toHaveCount(0);
    await page.getByTestId('redeem-sign-in').click();
    await expect(page).toHaveURL('/sign-in');
  });
}

test('rejects an unknown link', async ({ page }) => {
  await mockLink(page, { status: 404 });

  await page.goto('/link/tok');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('This link does not work');
});

test('shows the invite in Polish', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'invite' }, 'pl');

  await page.goto('/link/tok');

  await expect(page.getByTestId('redeem-title')).toHaveText('Alex zaprasza Cię do Spiżarni');
  await expect(page.getByTestId('redeem-create')).toHaveText('Dołącz i utwórz klucz');
});

test('accepts an invite with a username and password, without a passkey', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'invite' });
  const completed = page.waitForRequest('/api/passkey-links/redeem/tok/complete-with-password');
  await page.goto('/link/tok');

  await page.getByTestId('method-password').click();
  await page.getByLabel('Username').fill('sam');
  await page.getByLabel('Password', { exact: true }).fill('pantry shelf 2026');
  await page.getByLabel('Repeat the password').fill('pantry shelf 2026');
  await page.getByTestId('redeem-create').click();

  expect((await completed).postDataJSON()).toEqual({ displayName: 'Sam', userName: 'sam', password: 'pantry shelf 2026', uiLanguage: 'en' });
  await expect(page.getByTestId('redeem-done')).toHaveText('Welcome home!');
  await page.getByTestId('redeem-open-app').click();
  await expect(page).toHaveURL('/planner');
});

test('sets a new password from a recovery link', async ({ page }) => {
  await mockLink(page, { status: 200, kind: 'recovery', userName: 'sam' });
  const completed = page.waitForRequest('/api/passkey-links/redeem/tok/complete-with-password');
  await page.goto('/link/tok');

  await page.getByTestId('method-password').click();
  await expect(page.getByLabel('Username')).toHaveValue('sam');
  await page.getByLabel('Password', { exact: true }).fill('a brand new password');
  await page.getByLabel('Repeat the password').fill('a brand new password');
  await page.getByTestId('redeem-create').click();

  expect((await completed).postDataJSON()).toMatchObject({ userName: null, password: 'a brand new password' });
  await expect(page.getByTestId('redeem-done')).toBeVisible();
});
