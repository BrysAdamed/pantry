import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { registerUiLocales } from '../core/i18n/provide-i18n';
import { CurrentUser } from '../identity/identity-api';
import { PasskeyCancelledError, Passkeys } from '../identity/passkeys';
import { Session } from '../identity/session';
import { fakeMatchMedia } from '../testing/match-media';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { Passkey, PASSKEYS_URL } from './account-api';
import { AccountSection } from './account-section';

const phone: Passkey = { id: 'cGhvbmU', name: 'Phone', createdAt: '2026-11-08T10:00:00Z', lastUsedAt: '2026-11-09T08:00:00Z' };
const laptop: Passkey = { id: 'bGFwdG9w', name: null, createdAt: '2026-11-09T10:00:00Z', lastUsedAt: null };
const withPassword: CurrentUser = {
  id: '1',
  displayName: 'Alex',
  userName: 'alex',
  role: 'user',
  uiLanguage: null,
  theme: 'system',
  signInMethods: ['passkey', 'password'],
};

describe('AccountSection', () => {
  let http: HttpTestingController;
  let passkeys: { create: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    registerUiLocales();
    fakeMatchMedia();
    passkeys = { create: vi.fn().mockResolvedValue({ id: 'new' }) };
    await TestBed.configureTestingModule({
      imports: [AccountSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting(), { provide: Passkeys, useValue: passkeys }],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(list: Passkey[] = [phone, laptop]) {
    const fixture = TestBed.createComponent(AccountSection);
    fixture.detectChanges();
    http.expectOne(PASSKEYS_URL).flush(list);
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    await settle();
    const page = fixture.nativeElement as HTMLElement;
    const testIds = (id: string) => [...page.querySelectorAll<HTMLElement>(`[data-testid="${id}"]`)];
    const alert = () => page.querySelector('[role="alert"]')?.textContent ?? '';
    return { fixture, page, settle, testIds, alert };
  }

  it('lists the passkeys with when they were added and last used', async () => {
    const { testIds } = await render();

    const [first, second] = testIds('passkey').map((p) => p.textContent?.replace(/\s+/g, ' ').trim());
    expect(first).toContain('Phone');
    expect(first).toContain('Added 8 Nov 2026 · last used 9 Nov 2026');
    expect(second).toContain('Passkey');
    expect(second).toContain('not used to sign in yet');
  });

  it('does not offer to remove the only passkey', async () => {
    const { testIds } = await render([phone]);

    expect((testIds('passkey-remove')[0] as HTMLButtonElement).disabled).toBe(true);
    expect(testIds('passkey-only-one')[0].textContent).toContain('This is your only way to sign in.');
  });

  it('lets the only passkey go when the user has a password', async () => {
    TestBed.inject(Session).signedIn(withPassword);
    const { testIds } = await render([phone]);

    expect((testIds('passkey-remove')[0] as HTMLButtonElement).disabled).toBe(false);
    expect(testIds('passkey-only-one')).toHaveLength(0);
  });

  it('says when there is no passkey', async () => {
    TestBed.inject(Session).signedIn(withPassword);
    const { testIds } = await render([]);

    expect(testIds('passkeys-none')[0].textContent).toContain('No passkeys.');
  });

  it('adds a passkey created by the browser', async () => {
    const { testIds, settle } = await render([phone]);

    testIds('passkey-add')[0].click();
    await settle();
    http.expectOne({ method: 'POST', url: `${PASSKEYS_URL}/options` }).flush({ challenge: 'abc' });
    await settle();
    const added = http.expectOne({ method: 'POST', url: PASSKEYS_URL });
    expect(added.request.body).toEqual({ credential: { id: 'new' } });
    added.flush(laptop);
    await settle();

    expect(passkeys.create).toHaveBeenCalledWith({ challenge: 'abc' });
    expect(testIds('passkey')).toHaveLength(2);
    expect((testIds('passkey-remove')[0] as HTMLButtonElement).disabled).toBe(false);
  });

  it('offers to try again when the passkey window is closed', async () => {
    passkeys.create.mockRejectedValue(new PasskeyCancelledError());
    const { testIds, settle, alert } = await render([phone]);

    testIds('passkey-add')[0].click();
    await settle();
    http.expectOne(`${PASSKEYS_URL}/options`).flush({ challenge: 'abc' });
    await settle();

    expect(alert()).toContain('Adding the passkey was cancelled.');
    expect((testIds('passkey-add')[0] as HTMLButtonElement).disabled).toBe(false);
  });

  it('renames a passkey', async () => {
    const { testIds, page, settle } = await render();

    testIds('passkey-name')[1].click();
    await settle();
    const input = page.querySelector<HTMLInputElement>('input')!;
    input.value = ' Laptop ';
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await settle();
    const renamed = http.expectOne({ method: 'PUT', url: `${PASSKEYS_URL}/bGFwdG9w/name` });
    expect(renamed.request.body).toEqual({ name: 'Laptop' });
    renamed.flush(null);
    await settle();

    expect(testIds('passkey-name').map((b) => b.textContent?.trim())).toEqual(['Phone', 'Laptop']);
  });

  it('removes a passkey after confirmation', async () => {
    const { testIds, page, settle } = await render();

    testIds('passkey-remove')[0].click();
    await settle();
    expect(page.textContent).toContain('Remove Phone? Other devices will have to sign in again.');
    testIds('passkey-remove-confirm')[0].click();
    await settle();
    http.expectOne({ method: 'DELETE', url: `${PASSKEYS_URL}/cGhvbmU` }).flush(null);
    await settle();

    expect(testIds('passkey')).toHaveLength(1);
  });

  it('explains the last-passkey rule when the API refuses the removal', async () => {
    const { testIds, settle, alert } = await render();

    testIds('passkey-remove')[0].click();
    await settle();
    testIds('passkey-remove-confirm')[0].click();
    await settle();
    http.expectOne({ method: 'DELETE', url: `${PASSKEYS_URL}/cGhvbmU` }).flush({ title: 'last' }, { status: 409, statusText: 'Conflict' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    http.expectOne(PASSKEYS_URL).flush([phone]);
    await settle();

    expect(alert()).toContain('The last way to sign in has to stay.');
  });

  it('signs out other devices', async () => {
    const { testIds, page, settle } = await render();

    testIds('sign-out-others')[0].click();
    await settle();
    http.expectOne({ method: 'POST', url: '/api/me/sign-out-others' }).flush(null);
    await settle();

    expect(page.querySelector('[role="status"]')?.textContent).toContain('Other devices are signed out.');
  });
});
