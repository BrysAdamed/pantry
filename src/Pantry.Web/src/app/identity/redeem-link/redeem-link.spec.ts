import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { registerUiLocales } from '../../core/i18n/provide-i18n';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { CurrentUser, IdentityApi, RedeemableLink } from '../identity-api';
import { PasskeyCancelledError, Passkeys } from '../passkeys';
import { Session } from '../session';
import { RedeemLink } from './redeem-link';

const invite: RedeemableLink = { kind: 'invite', name: 'Sam', userName: null, invitedBy: 'Alex', expiresAt: '2026-11-11T10:00:00Z' };
const recovery: RedeemableLink = { kind: 'recovery', name: 'Sam', userName: null, invitedBy: 'Alex', expiresAt: '2026-11-11T10:00:00Z' };
const user: CurrentUser = { id: '2', displayName: 'Sam', role: 'user', userName: null, uiLanguage: null, theme: 'system', signInMethods: ['passkey'] };

describe('RedeemLink', () => {
  let api: Record<'redeemableLink' | 'redeemPasskeyOptions' | 'completeRedeem' | 'completeRedeemWithPassword' | 'refreshAntiforgeryToken', ReturnType<typeof vi.fn>>;
  let passkeys: { isSupported: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    registerUiLocales();
    api = {
      redeemableLink: vi.fn().mockResolvedValue(invite),
      redeemPasskeyOptions: vi.fn().mockResolvedValue({ challenge: 'abc' }),
      completeRedeem: vi.fn().mockResolvedValue(user),
      completeRedeemWithPassword: vi.fn().mockResolvedValue(user),
      refreshAntiforgeryToken: vi.fn().mockResolvedValue(undefined),
    };
    passkeys = { isSupported: vi.fn().mockReturnValue(true), create: vi.fn().mockResolvedValue({ id: 'cred' }) };
    await TestBed.configureTestingModule({
      imports: [RedeemLink],
      providers: [
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: IdentityApi, useValue: api },
        { provide: Passkeys, useValue: passkeys },
      ],
    }).compileComponents();
  });

  async function render() {
    const fixture = TestBed.createComponent(RedeemLink);
    fixture.componentRef.setInput('token', 'tok');
    const settle = async () => {
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    await settle();
    const page = fixture.nativeElement as HTMLElement;
    const testId = (id: string) => page.querySelector<HTMLElement>(`[data-testid="${id}"]`);
    const type = async (id: string, value: string) => {
      const input = testId(id) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));
      await fixture.whenStable();
    };
    const choosePassword = async () => {
      testId('method-password')!.click();
      await fixture.whenStable();
    };
    return { page, settle, testId, type, choosePassword };
  }

  it('shows an invite with the name to confirm', async () => {
    const { page, testId } = await render();

    expect(api.redeemableLink).toHaveBeenCalledWith('tok');
    expect(testId('redeem-title')?.textContent).toContain('Alex invites you to Pantry');
    expect(page.querySelector('input')!.value).toBe('Sam');
    expect(page.textContent).toContain('Valid until 11 Nov');
    expect(testId('redeem-create')?.textContent?.trim()).toBe('Join and create a passkey');
  });

  it('creates the passkey with the chosen name and signs the person in', async () => {
    const { page, settle, testId } = await render();
    const input = page.querySelector('input')!;
    input.value = ' Sam B ';
    input.dispatchEvent(new Event('input'));

    testId('redeem-create')!.click();
    await settle();

    expect(api.refreshAntiforgeryToken).toHaveBeenCalled();
    expect(api.redeemPasskeyOptions).toHaveBeenCalledWith('tok', 'Sam B');
    expect(api.completeRedeem).toHaveBeenCalledWith('tok', { id: 'cred' }, 'en');
    expect(testId('redeem-done')?.textContent).toBe('Welcome home!');
    expect(TestBed.inject(Session).user()).toEqual(user);
  });

  it('shows a recovery link without a name field', async () => {
    api.redeemableLink.mockResolvedValue(recovery);
    const { page, testId } = await render();

    expect(testId('redeem-title')?.textContent).toContain('Get back into Pantry, Sam');
    expect(testId('redeem-name')).toBeNull();
    expect((testId('redeem-create') as HTMLButtonElement).disabled).toBe(false);
  });

  it('offers to try again when the passkey window is closed', async () => {
    passkeys.create.mockRejectedValue(new PasskeyCancelledError());
    const { settle, testId } = await render();

    testId('redeem-create')!.click();
    await settle();

    expect(testId('redeem-error')?.textContent).toContain('Passkey creation cancelled.');
    expect(testId('redeem-create')?.textContent?.trim()).toBe('Try again');
  });

  it.each([
    [410, { state: 'expired' }, 'This link has expired'],
    [410, { state: 'used' }, 'This link was already used'],
    [410, { state: 'revoked' }, 'This link was cancelled'],
    [404, null, 'This link does not work'],
  ])('rejects a dead link (%s %o) with a clear screen', async (status, body, title) => {
    api.redeemableLink.mockRejectedValue(new HttpErrorResponse({ status, error: body }));
    const { testId } = await render();

    expect(testId('redeem-dead')?.textContent).toBe(title);
    expect(testId('redeem-create')).toBeNull();
    expect(testId('redeem-sign-in')).not.toBeNull();
  });

  it('shows the dead-link screen when the link is used up while the passkey is created', async () => {
    api.completeRedeem.mockRejectedValue(new HttpErrorResponse({ status: 410, error: { state: 'used' } }));
    const { settle, testId } = await render();

    testId('redeem-create')!.click();
    await settle();

    expect(testId('redeem-dead')?.textContent).toBe('This link was already used');
  });

  it('joins with a username and password instead of a passkey', async () => {
    const { settle, testId, type, choosePassword } = await render();
    await choosePassword();
    expect(testId('redeem-create')?.textContent?.trim()).toBe('Join');
    await type('new-user-name', 'sam');
    await type('new-password', 'pantry shelf 2026');
    await type('new-password-confirmation', 'pantry shelf 2026');

    testId('redeem-create')!.click();
    await settle();

    expect(passkeys.create).not.toHaveBeenCalled();
    expect(api.completeRedeemWithPassword).toHaveBeenCalledWith('tok', 'Sam', { userName: 'sam', password: 'pantry shelf 2026' }, 'en');
    expect(TestBed.inject(Session).user()).toEqual(user);
    expect(testId('redeem-done')?.textContent).toBe('Welcome home!');
  });

  it('tells when the chosen username is taken and lets the person pick another', async () => {
    api.completeRedeemWithPassword.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 400, error: { errors: { UserName: ['The username is taken.'] } } }),
    );
    const { settle, testId, type, choosePassword } = await render();
    await choosePassword();
    await type('new-user-name', 'alex');
    await type('new-password', 'pantry shelf 2026');
    await type('new-password-confirmation', 'pantry shelf 2026');

    testId('redeem-create')!.click();
    await settle();

    expect(testId('redeem-error')?.textContent).toContain('This username is taken.');
    expect((testId('redeem-create') as HTMLButtonElement).disabled).toBe(false);
  });

  it('sets a new password from a recovery link, keeping the known username', async () => {
    api.redeemableLink.mockResolvedValue({ ...recovery, userName: 'sam' });
    const { page, settle, testId, type, choosePassword } = await render();
    await choosePassword();

    expect(page.textContent).toContain('Set a new password. Your other devices will be signed out.');
    expect((testId('new-user-name') as HTMLInputElement).value).toBe('sam');
    expect((testId('new-user-name') as HTMLInputElement).readOnly).toBe(true);
    await type('new-password', 'a brand new password');
    await type('new-password-confirmation', 'a brand new password');
    testId('redeem-create')!.click();
    await settle();

    expect(api.completeRedeemWithPassword).toHaveBeenCalledWith('tok', '', { userName: null, password: 'a brand new password' }, 'en');
    expect(testId('redeem-done')).not.toBeNull();
  });

  it('forgets a typed password when the person switches to a passkey and back', async () => {
    const { settle, testId, type, choosePassword } = await render();
    await choosePassword();
    await type('new-user-name', 'sam');
    await type('new-password', 'pantry shelf 2026');
    await type('new-password-confirmation', 'pantry shelf 2026');
    expect((testId('redeem-create') as HTMLButtonElement).disabled).toBe(false);

    testId('method-passkey')!.click();
    await settle();
    expect(testId('new-password')).toBeNull();
    await choosePassword();

    expect((testId('new-password') as HTMLInputElement).value).toBe('');
    expect((testId('redeem-create') as HTMLButtonElement).disabled).toBe(true);
  });
});