import { expect, Page, test } from '@playwright/test';

const creationOptions = {
  rp: { id: 'localhost', name: 'localhost' },
  user: { id: 'dXNlci0x', name: 'Alex', displayName: 'Alex' },
  challenge: 'Y2hhbGxlbmdlLWNoYWxsZW5nZS1jaGFsbGVuZ2U',
  pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
  timeout: 60000,
  authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
  attestation: 'none',
};

const admin = { id: '1', displayName: 'Alex', role: 'admin', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };

async function mockSetup(page: Page, completeStatus = 200) {
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: true } }));
  await page.route('/api/me', (route) => route.fulfill({ status: 401 }));
  await page.route('/api/setup/passkey-options', (route) => route.fulfill({ json: creationOptions }));
  await page.route('/api/setup/complete-with-password', (route) =>
    route.fulfill({ json: { ...admin, userName: route.request().postDataJSON().userName, signInMethods: ['password'] } }),
  );
  await page.route('/api/setup/complete', (route) =>
    completeStatus === 200
      ? route.fulfill({ json: admin })
      : route.fulfill({ status: completeStatus, json: { title: 'First run is already done.' } }),
  );
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

async function closePasskeyWindow(page: Page) {
  await page.addInitScript(() => {
    navigator.credentials.create = () => Promise.reject(new DOMException('closed', 'NotAllowedError'));
  });
}

test('sends a fresh deployment to the first run', async ({ page }) => {
  await mockSetup(page);

  await page.goto('/');

  await expect(page).toHaveURL(/\/first-run$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText("Let's set up Pantry");
  await expect(page.getByTestId('first-run-create')).toBeDisabled();
});

test('creates the admin passkey and welcomes them home', async ({ page }) => {
  await mockSetup(page);
  await addVirtualAuthenticator(page);
  const completed = page.waitForRequest('/api/setup/complete');
  await page.goto('/first-run');

  await page.getByRole('textbox').fill('Alex');
  await page.getByTestId('first-run-create').click();

  const body = (await completed).postDataJSON();
  expect(body.credential.type).toBe('public-key');
  expect(body.credential.response.attestationObject).toBeTruthy();
  expect(body.uiLanguage).toBe('en');
  await expect(page.getByTestId('first-run-done')).toHaveText('Welcome home!');

  await page.getByTestId('first-run-open-app').click();
  await expect(page).toHaveURL('/planner');
});

test('offers to try again when the passkey window is closed', async ({ page }) => {
  await mockSetup(page);
  await closePasskeyWindow(page);
  await page.goto('/first-run');

  await page.getByRole('textbox').fill('Alex');
  await page.getByTestId('first-run-create').click();

  await expect(page.getByRole('alert')).toContainText('Passkey creation cancelled.');
  await expect(page.getByTestId('first-run-create')).toHaveText('Try again');
  await expect(page.getByTestId('first-run-create')).toBeEnabled();
});

test('tells when someone else finished the first run', async ({ page }) => {
  await mockSetup(page, 409);
  await addVirtualAuthenticator(page);
  await page.goto('/first-run');

  await page.getByRole('textbox').fill('Alex');
  await page.getByTestId('first-run-create').click();

  await expect(page.getByTestId('first-run-already-done')).toHaveText('Pantry is already set up');
});

test('shows the first run in Polish', async ({ page }) => {
  await mockSetup(page);
  await page.addInitScript(() => localStorage.setItem('pantry.uiLanguage', 'pl'));

  await page.goto('/first-run');

  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Załóżmy Spiżarnię');
  await expect(page.getByTestId('first-run-create')).toHaveText('Utwórz klucz dostępu');
});

test('skips the first run once an admin exists and asks to sign in', async ({ page }) => {
  await mockSetup(page);
  await page.route('/api/setup', (route) => route.fulfill({ json: { firstRunNeeded: false } }));

  await page.goto('/first-run');

  await expect(page).toHaveURL(/\/sign-in(\?|$)/);
});

test('sets up the admin with a username and password, without a passkey', async ({ page }) => {
  await mockSetup(page);
  const completed = page.waitForRequest('/api/setup/complete-with-password');
  await page.goto('/first-run');

  await page.getByLabel('What is your name?').fill('Alex');
  await page.getByTestId('method-password').click();
  await page.getByLabel('Username').fill('alex');
  await page.getByLabel('Password', { exact: true }).fill('pantry shelf 2026');
  await page.getByLabel('Repeat the password').fill('pantry shelf 2027');
  await page.getByLabel('Username').focus();
  await expect(page.getByText('The passwords do not match.')).toBeVisible();
  await expect(page.getByTestId('first-run-create')).toBeDisabled();
  await page.getByLabel('Repeat the password').fill('pantry shelf 2026');
  await page.getByTestId('first-run-create').click();

  expect((await completed).postDataJSON()).toEqual({ displayName: 'Alex', userName: 'alex', password: 'pantry shelf 2026', uiLanguage: 'en' });
  await expect(page.getByTestId('first-run-done')).toHaveText('Welcome home!');
  await expect(page.getByText('Next time sign in with your username and password.')).toBeVisible();
  await page.getByTestId('first-run-open-app').click();
  await expect(page).toHaveURL('/planner');
});

test('offers a password where the browser cannot create passkeys', async ({ page }) => {
  await mockSetup(page);
  await page.addInitScript(() => {
    Object.defineProperty(window, 'PublicKeyCredential', { value: undefined, configurable: true });
  });

  await page.goto('/first-run');

  await expect(page.getByTestId('method-password')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByTestId('first-run-create')).toHaveText('Create the account');
});
