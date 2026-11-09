import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { registerUiLocales } from '../../core/i18n/provide-i18n';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { Member, MEMBERS_URL, MembersResponse, stepPortionFactor } from '../household-api';
import { MembersSection } from './members-section';

const admin: Member = { id: 'a', name: 'Alex', portionFactor: 1, position: 0, account: 'admin' };
const toddler: Member = { id: 't', name: 'Toddler', portionFactor: 0.3, position: 1, account: null };

describe('MembersSection', () => {
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

  async function render(members: Member[] = [admin, toddler]) {
    const fixture = TestBed.createComponent(MembersSection);
    fixture.detectChanges();
    const response: MembersResponse = { members, householdServing: members.reduce((s, m) => s + m.portionFactor, 0) };
    http.expectOne(MEMBERS_URL).flush(response);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const button = (label: string) =>
      [...page.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label)!;
    const text = (testId: string) => [...page.querySelectorAll(`[data-testid="${testId}"]`)].map((e) => e.textContent?.trim());
    return { fixture, page, settle, button, text };
  }

  it('shows members with their factor, account badge and the household serving', async () => {
    const { page, text } = await render();

    expect(text('portion-factor')).toEqual(['1', '0.3']);
    expect(text('household-serving')).toEqual(['1.3']);
    expect(page.textContent).toContain('admin');
    expect(page.textContent).toContain('no account');
  });

  it('formats numbers with a decimal comma in Polish', async () => {
    TestBed.inject(TranslocoService).setActiveLang('pl');
    const { text } = await render();

    expect(text('portion-factor')).toEqual(['1', '0,3']);
    expect(text('household-serving')).toEqual(['1,3']);
  });

  it('updates the household serving at once and saves the new factor', async () => {
    const { button, text, settle } = await render();

    button('More for Toddler').click();
    await settle();

    expect(text('portion-factor')).toEqual(['1', '0.35']);
    expect(text('household-serving')).toEqual(['1.35']);
    const save = http.expectOne(`${MEMBERS_URL}/t`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ name: 'Toddler', portionFactor: 0.35 });
    save.flush({ ...toddler, portionFactor: 0.35 });
  });

  it('sends quick changes of one member in order', async () => {
    const { button, settle } = await render();

    button('More for Alex').click();
    button('More for Alex').click();
    await settle();

    http.expectOne(`${MEMBERS_URL}/a`).flush({ ...admin, portionFactor: 1.05 });
    await settle();
    expect(http.expectOne(`${MEMBERS_URL}/a`).request.body.portionFactor).toBe(1.1);
  });

  it('stops the stepper at the factor bounds', async () => {
    const { button } = await render([
      { ...admin, portionFactor: 2 },
      { ...toddler, portionFactor: 0.05 },
    ]);

    expect(button('More for Alex').disabled).toBe(true);
    expect(button('Less for Alex').disabled).toBe(false);
    expect(button('Less for Toddler').disabled).toBe(true);
  });

  it('adds a member with the default factor', async () => {
    const { page, settle, text } = await render();
    const input = page.querySelector<HTMLInputElement>('form input')!;

    input.value = '  Guest ';
    input.dispatchEvent(new Event('input'));
    page.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle();

    const add = http.expectOne({ method: 'POST', url: MEMBERS_URL });
    expect(add.request.body).toEqual({ name: 'Guest', portionFactor: 1 });
    add.flush({ id: 'g', name: 'Guest', portionFactor: 1, position: 2, account: null });
    await settle();
    expect(text('household-serving')).toEqual(['2.3']);
    expect(input.value).toBe('');
  });

  it('removes a member without an account after confirmation', async () => {
    const { page, button, settle } = await render();

    expect(button('Remove Alex')).toBeUndefined();
    button('Remove Toddler').click();
    await settle();
    expect(page.textContent).toContain('Remove Toddler?');
    button('Remove').click();
    await settle();

    http.expectOne({ method: 'DELETE', url: `${MEMBERS_URL}/t` }).flush(null);
    await settle();
    expect(page.querySelectorAll('[data-testid="member"]')).toHaveLength(1);
  });

  it('reports a failed save and reloads the members', async () => {
    const { page, button, settle, text } = await render();

    button('More for Toddler').click();
    await settle();
    http.expectOne(`${MEMBERS_URL}/t`).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();

    http.expectOne(MEMBERS_URL).flush({ members: [admin, toddler], householdServing: 1.3 });
    await settle();
    expect(text('portion-factor')).toEqual(['1', '0.3']);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The change was not saved');
  });
});

describe('stepPortionFactor', () => {
  it('steps by 0.05 without floating point drift and clamps to the bounds', () => {
    expect(stepPortionFactor(0.3, 1)).toBe(0.35);
    expect(stepPortionFactor(0.1, -1)).toBe(0.05);
    expect(stepPortionFactor(0.05, -1)).toBe(0.05);
    expect(stepPortionFactor(2, 1)).toBe(2);
  });
});
