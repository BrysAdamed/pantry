import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { registerUiLocales } from '../../core/i18n/provide-i18n';
import { Session } from '../../identity/session';
import { OpenPasskeyLink, PASSKEY_LINKS_URL } from '../../identity/user-admin-api';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { Member, MEMBERS_URL } from '../household-api';
import { MembersSection } from './members-section';

const admin: Member = { id: 'a', name: 'Alex', portionFactor: 1, position: 0, account: 'admin', userId: 'u-a' };
const toddler: Member = { id: 't', name: 'Toddler', portionFactor: 0.3, position: 1, account: null, userId: null };
const partner: Member = { id: 'p', name: 'Sam', portionFactor: 1, position: 2, account: 'user', userId: 'u-p' };

const invite: OpenPasskeyLink = {
  id: 'l1',
  kind: 'invite',
  memberId: 't',
  userId: null,
  createdAt: '2026-11-09T10:00:00Z',
  expiresAt: '2026-11-11T10:00:00Z',
};

describe('MembersSection account administration', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    registerUiLocales();
    await TestBed.configureTestingModule({
      imports: [MembersSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(role: 'admin' | 'user', links: OpenPasskeyLink[] = []) {
    TestBed.inject(Session).signedIn({ id: 'u-a', displayName: 'Alex', role, uiLanguage: null, theme: 'system' });
    const fixture = TestBed.createComponent(MembersSection);
    fixture.detectChanges();
    http.expectOne(MEMBERS_URL).flush({ members: [admin, toddler, partner], householdServing: 2.3 });
    if (role === 'admin') {
      http.expectOne(PASSKEY_LINKS_URL).flush(links);
    }
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const tick = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
    };
    const row = (name: string) => page.querySelector<HTMLElement>(`[role="group"][aria-label="${name}"]`)!;
    const button = (name: string, label: string) =>
      [...row(name).querySelectorAll<HTMLButtonElement>('button')].find((b) => b.textContent?.trim() === label);
    return { fixture, page, settle, tick, row, button };
  }

  it('shows no account actions to a user who is not an admin', async () => {
    const { page } = await render('user');

    expect(page.textContent).not.toContain('Invite');
    expect(page.textContent).not.toContain('Recover access');
    expect(page.textContent).not.toContain('Remove account');
  });

  it('offers an invite for members without an account and account actions for other users, not for oneself', async () => {
    const { button, row } = await render('admin');

    expect(button('Toddler', 'Invite')).toBeDefined();
    expect(button('Sam', 'Recover access')).toBeDefined();
    expect(button('Sam', 'Make admin')).toBeDefined();
    expect(button('Sam', 'Remove account')).toBeDefined();
    expect(row('Alex').textContent?.trim()).toBe('');
  });

  it('issues an invite and shows the link to copy', async () => {
    const { page, button, settle, tick } = await render('admin');
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    button('Toddler', 'Invite')!.click();
    await settle();
    const issue = http.expectOne({ method: 'POST', url: `${PASSKEY_LINKS_URL}/invites` });
    expect(issue.request.body).toEqual({ memberId: 't' });
    issue.flush({ ...invite, token: 'secret-token' });
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([invite]);
    await settle();

    const panel = page.querySelector('[data-testid="issued-link"]')!;
    expect(panel.textContent).toContain('Invite for Toddler');
    expect(page.querySelector<HTMLInputElement>('[data-testid="issued-link-url"]')!.value).toBe(`${location.origin}/link/secret-token`);
    expect(page.textContent).toContain('invite sent');
    expect(button('Toddler', 'New link')).toBeDefined();

    page.querySelector<HTMLButtonElement>('[data-testid="issued-link-copy"]')!.click();
    await settle();
    expect(writeText).toHaveBeenCalledWith(`${location.origin}/link/secret-token`);
    expect(panel.textContent).toContain('Copied');
  });

  it('revokes an open invite', async () => {
    const { button, settle, tick } = await render('admin', [invite]);

    button('Toddler', 'Revoke link')!.click();
    await settle();
    http.expectOne({ method: 'DELETE', url: `${PASSKEY_LINKS_URL}/l1` }).flush(null);
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(button('Toddler', 'Invite')).toBeDefined();
  });

  it('issues a recovery link for a user', async () => {
    const { page, button, settle, tick } = await render('admin');

    button('Sam', 'Recover access')!.click();
    await settle();
    const issue = http.expectOne({ method: 'POST', url: `${PASSKEY_LINKS_URL}/recovery` });
    expect(issue.request.body).toEqual({ userId: 'u-p' });
    issue.flush({ id: 'l2', kind: 'recovery', token: 'tok', memberId: null, userId: 'u-p', expiresAt: invite.expiresAt });
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(page.querySelector('[data-testid="issued-link"]')!.textContent).toContain('Recovery link for Sam');
  });

  it('promotes a user to admin', async () => {
    const { page, button, settle, tick } = await render('admin');

    button('Sam', 'Make admin')!.click();
    await settle();
    const change = http.expectOne({ method: 'PUT', url: '/api/users/u-p/role' });
    expect(change.request.body).toEqual({ role: 'admin' });
    change.flush(null);
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(button('Sam', 'Remove admin role')).toBeDefined();
    expect(page.querySelectorAll('[data-testid="member"]')[2].textContent).toContain('admin');
  });

  it('removes a user after confirmation and keeps the member', async () => {
    const { page, button, settle, tick } = await render('admin');

    button('Sam', 'Remove account')!.click();
    await settle();
    expect(page.textContent).toContain('Remove the account of Sam? The member stays.');
    button('Sam', 'Remove account')!.click();
    await settle();
    http.expectOne({ method: 'DELETE', url: '/api/users/u-p' }).flush(null);
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(page.querySelectorAll('[data-testid="member"]')).toHaveLength(3);
    expect(button('Sam', 'Invite')).toBeDefined();
  });

  it('explains the last-admin guard', async () => {
    const { page, button, settle, tick } = await render('admin');

    button('Sam', 'Remove account')!.click();
    await settle();
    button('Sam', 'Remove account')!.click();
    await settle();
    http.expectOne({ method: 'DELETE', url: '/api/users/u-p' }).flush({ title: 'last admin' }, { status: 409, statusText: 'Conflict' });
    await tick();
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The last admin has to stay.');
  });

  it('reads the members again when an invite finds the member already has an account', async () => {
    const { page, button, settle, tick } = await render('admin');

    button('Toddler', 'Invite')!.click();
    await settle();
    http
      .expectOne({ method: 'POST', url: `${PASSKEY_LINKS_URL}/invites` })
      .flush({ title: 'member already has an account' }, { status: 409, statusText: 'Conflict' });
    await tick();
    http.expectOne(MEMBERS_URL).flush({ members: [admin, { ...toddler, account: 'user', userId: 'u-t' }, partner], householdServing: 2.3 });
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    const alert = page.querySelector('[role="alert"]')?.textContent;
    expect(alert).toContain('Someone changed this in the meantime.');
    expect(alert).not.toContain('The last admin has to stay.');
    expect(button('Toddler', 'Recover access')).toBeDefined();
  });

  it('reads the links again when a revoked link was already used', async () => {
    const { page, button, settle, tick } = await render('admin', [invite]);

    button('Toddler', 'Revoke link')!.click();
    await settle();
    http
      .expectOne({ method: 'DELETE', url: `${PASSKEY_LINKS_URL}/l1` })
      .flush({ title: 'link already used' }, { status: 409, statusText: 'Conflict' });
    await tick();
    http.expectOne(MEMBERS_URL).flush({ members: [admin, toddler, partner], householdServing: 2.3 });
    http.expectOne(PASSKEY_LINKS_URL).flush([]);
    await settle();

    expect(page.querySelector('[role="alert"]')?.textContent).not.toContain('The last admin has to stay.');
    expect(button('Toddler', 'Revoke link')).toBeUndefined();
  });
});
