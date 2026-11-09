import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CurrentUser } from '../identity/identity-api';
import { Session } from '../identity/session';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { PASSWORD_URL } from './account-api';
import { PasswordSection } from './password-section';

const passkeyOnly: CurrentUser = {
  id: '1',
  displayName: 'Alex',
  userName: null,
  role: 'user',
  uiLanguage: null,
  theme: 'system',
  signInMethods: ['passkey'],
};
const both: CurrentUser = { ...passkeyOnly, userName: 'alex', signInMethods: ['passkey', 'password'] };
const passwordOnly: CurrentUser = { ...both, signInMethods: ['password'] };

describe('PasswordSection', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PasswordSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(user: CurrentUser, hasPasskey = true) {
    TestBed.inject(Session).signedIn(user);
    const fixture = TestBed.createComponent(PasswordSection);
    fixture.componentRef.setInput('hasPasskey', hasPasskey);
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    await settle();
    const page = fixture.nativeElement as HTMLElement;
    const testId = <T extends HTMLElement = HTMLElement>(id: string) => page.querySelector<T>(`[data-testid="${id}"]`);
    const click = async (id: string) => {
      testId(id)!.click();
      await settle();
    };
    const type = async (id: string, value: string) => {
      const input = testId<HTMLInputElement>(id)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));
      await fixture.whenStable();
    };
    const fillNewPassword = async (password: string) => {
      await type('new-password', password);
      await type('new-password-confirmation', password);
    };
    const alert = () => page.querySelector('[role="alert"]')?.textContent ?? '';
    const status = () => page.querySelector('[role="status"]')?.textContent ?? '';
    return { page, settle, testId, click, type, fillNewPassword, alert, status };
  }

  it('sets a first password together with a username and signs out other devices', async () => {
    const { testId, click, type, fillNewPassword, settle, status } = await render(passkeyOnly);
    expect(testId('account-user-name')?.textContent).toContain('none yet');
    expect(testId('account-password-state')?.textContent).toContain('not set');

    await click('password-set');
    await type('new-user-name', 'alex');
    await fillNewPassword('pantry shelf 2026');
    await click('password-save');
    const request = http.expectOne({ method: 'POST', url: PASSWORD_URL });
    expect(request.request.body).toEqual({ userName: 'alex', password: 'pantry shelf 2026' });
    request.flush(both);
    await settle();

    expect(status()).toContain('Password saved. Other devices are signed out.');
    expect(testId('account-user-name')?.textContent).toContain('alex');
    expect(TestBed.inject(Session).user()?.signInMethods).toEqual(['passkey', 'password']);
  });

  it('tells when the username is taken and keeps the form', async () => {
    const { testId, click, type, fillNewPassword, settle, alert } = await render(passkeyOnly);

    await click('password-set');
    await type('new-user-name', 'alex');
    await fillNewPassword('pantry shelf 2026');
    await click('password-save');
    http
      .expectOne({ method: 'POST', url: PASSWORD_URL })
      .flush({ errors: { UserName: ['The username is taken.'] } }, { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(alert()).toContain('This username is taken.');
    expect(testId('password-form')).not.toBeNull();
  });

  it('changes the password with the current one', async () => {
    const { testId, click, type, fillNewPassword, settle, status } = await render(both);

    await click('password-change');
    expect(testId<HTMLInputElement>('new-user-name')?.value).toBe('alex');
    await type('current-password', 'pantry shelf 2026');
    await fillNewPassword('a brand new password');
    await click('password-save');
    const request = http.expectOne({ method: 'PUT', url: PASSWORD_URL });
    expect(request.request.body).toEqual({ currentPassword: 'pantry shelf 2026', newPassword: 'a brand new password' });
    request.flush(both);
    await settle();

    expect(status()).toContain('Password saved.');
    expect(testId('password-form')).toBeNull();
  });

  it('tells when the current password is wrong', async () => {
    const { click, type, fillNewPassword, settle, alert } = await render(both);

    await click('password-change');
    await type('current-password', 'not it at all');
    await fillNewPassword('a brand new password');
    await click('password-save');
    http
      .expectOne({ method: 'PUT', url: PASSWORD_URL })
      .flush({ errors: { CurrentPassword: ['wrong'] } }, { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(alert()).toContain('The current password is wrong.');
  });

  it('removes the password after confirmation', async () => {
    const { page, click, settle, status, testId } = await render(both);

    await click('password-remove');
    expect(page.textContent).toContain('Remove the password? Other devices will have to sign in again.');
    await click('password-remove-confirm');
    http.expectOne({ method: 'DELETE', url: PASSWORD_URL }).flush({ ...both, signInMethods: ['passkey'] });
    await settle();

    expect(status()).toContain('Password removed.');
    expect(testId('password-set')).not.toBeNull();
  });

  it('keeps the password of a user without a passkey', async () => {
    const { testId } = await render(passwordOnly, false);

    expect(testId<HTMLButtonElement>('password-remove')!.disabled).toBe(true);
    expect(testId('password-only-method')?.textContent).toContain('The password is your only way to sign in.');
  });

  it('explains the last-method rule when the API refuses the removal', async () => {
    const { click, settle, alert } = await render(both);

    await click('password-remove');
    await click('password-remove-confirm');
    http.expectOne({ method: 'DELETE', url: PASSWORD_URL }).flush({ title: 'last' }, { status: 409, statusText: 'Conflict' });
    await settle();
    http.expectOne('/api/me').flush(passwordOnly);
    await settle();

    expect(alert()).toContain('The last way to sign in has to stay.');
  });
});
