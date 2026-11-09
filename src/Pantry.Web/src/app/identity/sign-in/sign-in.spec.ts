import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { SignIn } from './sign-in';
import { CurrentUser, IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys } from '../passkeys';
import { Session } from '../session';
import { provideTranslocoTesting } from '../../testing/transloco-testing';

const user: CurrentUser = { id: '1', displayName: 'Alex', role: 'user', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };

describe('SignIn', () => {
  let api: Record<'refreshAntiforgeryToken' | 'signInPasskeyOptions' | 'signIn' | 'signInWithPassword', ReturnType<typeof vi.fn>>;
  let passkeys: Record<'isSupported' | 'isAutofillSupported' | 'get', ReturnType<typeof vi.fn>>;

  beforeEach(async () => {
    api = {
      refreshAntiforgeryToken: vi.fn().mockResolvedValue(undefined),
      signInPasskeyOptions: vi.fn().mockResolvedValue({ challenge: 'abc' }),
      signIn: vi.fn().mockResolvedValue(user),
      signInWithPassword: vi.fn().mockResolvedValue(user),
    };
    passkeys = {
      isSupported: vi.fn().mockReturnValue(true),
      isAutofillSupported: vi.fn().mockResolvedValue(false),
      get: vi.fn().mockResolvedValue({ id: 'cred' }),
    };
    await TestBed.configureTestingModule({
      imports: [SignIn],
      providers: [
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: IdentityApi, useValue: api },
        { provide: Passkeys, useValue: passkeys },
      ],
    }).compileComponents();
  });

  const settle = () => new Promise((resolve) => setTimeout(resolve));

  async function render(inputs: { returnUrl?: string; reason?: string } = {}) {
    const fixture = TestBed.createComponent(SignIn);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    await settle();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const button = () => page.querySelector<HTMLButtonElement>('[data-testid="sign-in-passkey"]')!;
    const signIn = async () => {
      button().click();
      await settle();
      await fixture.whenStable();
    };
    const type = async (id: string, value: string) => {
      const input = page.querySelector<HTMLInputElement>(`[data-testid="${id}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const submitPassword = async () => {
      page.querySelector<HTMLButtonElement>('[data-testid="sign-in-with-password"]')!.click();
      await settle();
      await fixture.whenStable();
    };
    return { fixture, page, button, signIn, type, submitPassword };
  }

  it('signs in with a fresh token and returns to the requested page', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { signIn } = await render({ returnUrl: '/planner' });

    await signIn();

    expect(api.refreshAntiforgeryToken).toHaveBeenCalledBefore(api.signInPasskeyOptions);
    expect(passkeys.get).toHaveBeenCalledWith({ challenge: 'abc' });
    expect(api.signIn).toHaveBeenCalledWith({ id: 'cred' });
    expect(TestBed.inject(Session).user()).toEqual(user);
    expect(navigate).toHaveBeenCalledWith('/planner', { replaceUrl: true });
  });

  it('ignores a return URL outside the app', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { signIn } = await render({ returnUrl: '//evil.example' });

    await signIn();

    expect(navigate).toHaveBeenCalledWith('/', { replaceUrl: true });
  });

  it('offers to try again when the passkey window is closed', async () => {
    passkeys.get.mockRejectedValueOnce(new PasskeyCancelledError());
    const { page, button, signIn } = await render();

    await signIn();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Sign-in interrupted.');
    expect(button().textContent).toContain('Try again');
    expect(button().disabled).toBe(false);
  });

  it('tells when the passkey is not known', async () => {
    api.signIn.mockRejectedValueOnce(new HttpErrorResponse({ status: 401 }));
    const { page, signIn } = await render();

    await signIn();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('This passkey is not known here.');
  });

  it('tells that the session expired', async () => {
    const { page } = await render({ reason: 'expired' });

    expect(page.querySelector('[data-testid="sign-in-expired"]')?.textContent).toContain('Your session has expired.');
  });

  it('explains when the browser cannot use passkeys', async () => {
    passkeys.isSupported.mockReturnValue(false);
    const { page, button } = await render();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Passkeys do not work here.');
    expect(button().disabled).toBe(true);
  });

  it('offers passkeys in the autofill and signs in with the picked one', async () => {
    passkeys.isAutofillSupported.mockResolvedValue(true);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    const { page } = await render();

    expect(page.querySelector('input')?.getAttribute('autocomplete')).toBe('username webauthn');
    expect(passkeys.get).toHaveBeenCalledWith({ challenge: 'abc' }, expect.objectContaining({ conditional: true }));
    expect(navigate).toHaveBeenCalledWith('/', { replaceUrl: true });
  });

  it('aborts the autofill request when the button is used', async () => {
    passkeys.isAutofillSupported.mockResolvedValue(true);
    let signal: AbortSignal | undefined;
    passkeys.get.mockImplementation((_options, { conditional, signal: s } = {}) => {
      if (conditional) {
        signal = s;
        return new Promise(() => undefined);
      }
      return Promise.resolve({ id: 'cred' });
    });
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { signIn } = await render();

    await signIn();

    expect(signal?.aborted).toBe(true);
    expect(api.signIn).toHaveBeenCalledWith({ id: 'cred' });
  });

  it('signs in with a username and password and returns to the requested page', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { type, submitPassword } = await render({ returnUrl: '/shopping' });

    await type('sign-in-user-name', ' alex ');
    await type('sign-in-password', 'pantry shelf 2026');
    await submitPassword();

    expect(api.refreshAntiforgeryToken).toHaveBeenCalledBefore(api.signInWithPassword);
    expect(api.signInWithPassword).toHaveBeenCalledWith('alex', 'pantry shelf 2026');
    expect(TestBed.inject(Session).user()).toEqual(user);
    expect(navigate).toHaveBeenCalledWith('/shopping', { replaceUrl: true });
  });

  it('offers the password form only when both fields are filled', async () => {
    const { page, type } = await render();
    const submit = () => page.querySelector<HTMLButtonElement>('[data-testid="sign-in-with-password"]')!;

    expect(submit().disabled).toBe(true);
    await type('sign-in-user-name', 'alex');
    expect(submit().disabled).toBe(true);
    await type('sign-in-password', 'x');

    expect(submit().disabled).toBe(false);
    expect(page.querySelector('[data-testid="sign-in-password"]')?.getAttribute('autocomplete')).toBe('current-password');
  });

  it('gives one message for a wrong username, a wrong password and a locked account', async () => {
    api.signInWithPassword.mockRejectedValueOnce(new HttpErrorResponse({ status: 401 }));
    const { page, type, submitPassword } = await render();

    await type('sign-in-user-name', 'alex');
    await type('sign-in-password', 'wrong password');
    await submitPassword();

    expect(page.querySelectorAll('[role="alert"]')).toHaveLength(1);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Wrong username or password.');
  });

  it('replaces a passkey message with the password one, never both', async () => {
    passkeys.get.mockRejectedValueOnce(new PasskeyCancelledError());
    api.signInWithPassword.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const { page, button, signIn, type, submitPassword } = await render();
    await signIn();

    await type('sign-in-user-name', 'alex');
    await type('sign-in-password', 'pantry shelf 2026');
    await submitPassword();

    expect(page.querySelectorAll('[role="alert"]')).toHaveLength(1);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Could not sign in.');
    expect(button().textContent).toContain('Sign in with a passkey');
  });

  it('still signs in with a password where passkeys do not work', async () => {
    passkeys.isSupported.mockReturnValue(false);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const { type, submitPassword } = await render();

    await type('sign-in-user-name', 'alex');
    await type('sign-in-password', 'pantry shelf 2026');
    await submitPassword();

    expect(api.signInWithPassword).toHaveBeenCalledWith('alex', 'pantry shelf 2026');
  });
});