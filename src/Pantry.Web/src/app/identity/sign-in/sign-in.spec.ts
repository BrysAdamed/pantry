import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { SignIn } from './sign-in';
import { CurrentUser, IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys } from '../passkeys';
import { Session } from '../session';
import { provideTranslocoTesting } from '../../testing/transloco-testing';

const user: CurrentUser = { id: '1', displayName: 'Alex', role: 'user', uiLanguage: null, theme: 'system' };

describe('SignIn', () => {
  let api: Record<'refreshAntiforgeryToken' | 'signInPasskeyOptions' | 'signIn', ReturnType<typeof vi.fn>>;
  let passkeys: Record<'isSupported' | 'isAutofillSupported' | 'get', ReturnType<typeof vi.fn>>;

  beforeEach(async () => {
    api = {
      refreshAntiforgeryToken: vi.fn().mockResolvedValue(undefined),
      signInPasskeyOptions: vi.fn().mockResolvedValue({ challenge: 'abc' }),
      signIn: vi.fn().mockResolvedValue(user),
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
    return { fixture, page, button, signIn };
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
});
