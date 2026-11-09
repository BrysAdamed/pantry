import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { FirstRun } from './first-run';
import { CurrentUser, IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys } from '../passkeys';
import { SetupStatusService } from '../setup-status';
import { provideTranslocoTesting } from '../../testing/transloco-testing';

const admin: CurrentUser = { id: '1', displayName: 'Alex', role: 'admin', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };

describe('FirstRun', () => {
  let api: Record<'firstRunPasskeyOptions' | 'completeFirstRun' | 'completeFirstRunWithPassword', ReturnType<typeof vi.fn>>;
  let passkeys: { isSupported: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = {
      firstRunPasskeyOptions: vi.fn().mockResolvedValue({ challenge: 'abc' }),
      completeFirstRun: vi.fn().mockResolvedValue(admin),
      completeFirstRunWithPassword: vi.fn().mockResolvedValue(admin),
    };
    passkeys = { isSupported: vi.fn().mockReturnValue(true), create: vi.fn().mockResolvedValue({ id: 'cred' }) };
    await TestBed.configureTestingModule({
      imports: [FirstRun],
      providers: [
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: IdentityApi, useValue: api },
        { provide: Passkeys, useValue: passkeys },
      ],
    }).compileComponents();
  });

  async function render() {
    const fixture = TestBed.createComponent(FirstRun);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const button = () => page.querySelector<HTMLButtonElement>('[data-testid="first-run-create"]')!;
    const type = (value: string) => typeInto('first-run-name', value);
    const typeInto = async (id: string, value: string) => {
      const input = page.querySelector<HTMLInputElement>(`[data-testid="${id}"]`)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));
      await fixture.whenStable();
    };
    const choose = async (method: 'passkey' | 'password') => {
      page.querySelector<HTMLButtonElement>(`[data-testid="method-${method}"]`)!.click();
      await fixture.whenStable();
    };
    const create = async () => {
      button().click();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    return { page, button, type, typeInto, choose, create };
  }

  it('enables passkey creation only after a name is typed', async () => {
    const { button, type } = await render();
    expect(button().disabled).toBe(true);

    await type('  Alex ');

    expect(button().disabled).toBe(false);
  });

  it('creates the passkey with the trimmed name and welcomes the admin', async () => {
    const { page, type, create } = await render();
    await type('  Alex ');

    await create();

    expect(api.firstRunPasskeyOptions).toHaveBeenCalledWith('Alex');
    expect(passkeys.create).toHaveBeenCalledWith({ challenge: 'abc' });
    expect(api.completeFirstRun).toHaveBeenCalledWith({ id: 'cred' }, 'en');
    expect(page.querySelector('[data-testid="first-run-done"]')?.textContent).toContain('Welcome home!');
  });

  it('offers to try again when the passkey window is closed', async () => {
    passkeys.create.mockRejectedValueOnce(new PasskeyCancelledError());
    const { page, button, type, create } = await render();
    await type('Alex');

    await create();

    expect(page.querySelector('[data-testid="first-run-error"]')?.textContent).toContain('Passkey creation cancelled.');
    expect(button().textContent).toContain('Try again');
    expect(api.completeFirstRun).not.toHaveBeenCalled();
  });

  it('tells that the first run is already done on 409', async () => {
    api.completeFirstRun.mockRejectedValueOnce(new HttpErrorResponse({ status: 409 }));
    const { page, type, create } = await render();
    await type('Alex');

    await create();

    expect(page.querySelector('[data-testid="first-run-already-done"]')).not.toBeNull();
  });

  it('picks a password where the browser cannot create passkeys, and explains why', async () => {
    passkeys.isSupported.mockReturnValue(false);
    const { page, button, type, choose } = await render();
    await type('Alex');

    expect(page.querySelector('[data-testid="method-password"]')?.getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('[data-testid="new-password"]')).not.toBeNull();
    await choose('passkey');

    expect(page.querySelector('[data-testid="first-run-error"]')?.textContent).toContain('Passkeys do not work here.');
    expect(button().disabled).toBe(true);
  });

  it('creates the admin with a username and password instead of a passkey', async () => {
    const { page, button, type, typeInto, choose, create } = await render();
    await type(' Alex ');
    await choose('password');
    expect(button().textContent?.trim()).toBe('Create the account');
    await typeInto('new-user-name', 'alex');
    await typeInto('new-password', 'pantry shelf 2026');
    expect(button().disabled).toBe(true);
    await typeInto('new-password-confirmation', 'pantry shelf 2026');

    await create();

    expect(passkeys.create).not.toHaveBeenCalled();
    expect(api.completeFirstRunWithPassword).toHaveBeenCalledWith('Alex', { userName: 'alex', password: 'pantry shelf 2026' }, 'en');
    expect(page.querySelector('[data-testid="first-run-done"]')?.textContent).toContain('Welcome home!');
    expect(page.textContent).toContain('Next time sign in with your username and password.');
  });

  it('offers to try again when the account cannot be created with a password', async () => {
    api.completeFirstRunWithPassword.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const { page, button, type, typeInto, choose, create } = await render();
    await type('Alex');
    await choose('password');
    await typeInto('new-user-name', 'alex');
    await typeInto('new-password', 'pantry shelf 2026');
    await typeInto('new-password-confirmation', 'pantry shelf 2026');

    await create();

    expect(page.querySelector('[data-testid="first-run-error"]')?.textContent).toContain('The account could not be created.');
    expect(button().textContent?.trim()).toBe('Try again');
  });

  it('opens the app after the first run', async () => {
    const { page, type, create } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    await type('Alex');
    await create();

    page.querySelector<HTMLButtonElement>('[data-testid="first-run-open-app"]')!.click();

    expect(navigate).toHaveBeenCalledWith('/');
    expect(await TestBed.inject(SetupStatusService).firstRunNeeded()).toBe(false);
  });

  it('forgets a typed password when the person switches to a passkey and back', async () => {
    const { page, button, type, typeInto, choose } = await render();
    await type('Alex');
    await choose('password');
    await typeInto('new-user-name', 'alex');
    await typeInto('new-password', 'pantry shelf 2026');
    await typeInto('new-password-confirmation', 'pantry shelf 2026');
    expect(button().disabled).toBe(false);

    await choose('passkey');
    await choose('password');

    expect(page.querySelector<HTMLInputElement>('[data-testid="new-password"]')!.value).toBe('');
    expect(button().disabled).toBe(true);
  });
});