import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { FirstRun } from './first-run';
import { CurrentUser, IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys } from '../passkeys';
import { SetupStatusService } from '../setup-status';
import { provideTranslocoTesting } from '../../testing/transloco-testing';

const admin: CurrentUser = { id: '1', displayName: 'Alex', role: 'admin', uiLanguage: null, theme: 'system' };

describe('FirstRun', () => {
  let api: { firstRunPasskeyOptions: ReturnType<typeof vi.fn>; completeFirstRun: ReturnType<typeof vi.fn> };
  let passkeys: { isSupported: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    api = {
      firstRunPasskeyOptions: vi.fn().mockResolvedValue({ challenge: 'abc' }),
      completeFirstRun: vi.fn().mockResolvedValue(admin),
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
    const type = async (value: string) => {
      const input = page.querySelector('input')!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const create = async () => {
      button().click();
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    return { page, button, type, create };
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

  it('explains when the browser cannot create passkeys', async () => {
    passkeys.isSupported.mockReturnValue(false);
    const { page, button } = await render();

    expect(page.querySelector('[data-testid="first-run-error"]')?.textContent).toContain('Passkeys do not work here.');
    expect(button().disabled).toBe(true);
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
});
