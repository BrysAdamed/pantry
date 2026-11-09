import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { PLANNING_RULES_URL, PlanningRule } from '../household-api';
import { PlanningRulesSection } from './planning-rules-section';

const quick: PlanningRule = { id: 'q', text: 'Weekday dinners under 30 minutes', position: 0, isEnabled: true };
const frying: PlanningRule = { id: 'f', text: 'No deep frying', position: 1, isEnabled: false };
const fish: PlanningRule = { id: 'h', text: 'Fish at least once a week', position: 2, isEnabled: true };

describe('PlanningRulesSection', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PlanningRulesSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(rules: PlanningRule[] = [quick, frying, fish]) {
    const fixture = TestBed.createComponent(PlanningRulesSection);
    fixture.detectChanges();
    http.expectOne(PLANNING_RULES_URL).flush(rules);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const button = (label: string) =>
      [...page.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label)!;
    const texts = () => [...page.querySelectorAll<HTMLTextAreaElement>('[data-testid="planning-rule"] textarea')].map((i) => i.value);
    const switches = () => [...page.querySelectorAll<HTMLButtonElement>('[role="switch"]')];
    const note = () => page.querySelector('[data-testid="planning-rules-note"]')?.textContent?.trim();
    return { page, settle, button, texts, switches, note };
  }

  it('shows the rules in order with their switches and how many the agent reads', async () => {
    const { texts, switches, note } = await render();

    expect(texts()).toEqual(['Weekday dinners under 30 minutes', 'No deep frying', 'Fish at least once a week']);
    expect(switches().map((s) => s.getAttribute('aria-checked'))).toEqual(['true', 'false', 'true']);
    expect(switches()[1].getAttribute('aria-label')).toBe('Rule 2 on');
    expect(note()).toBe('Rules the agent reads: 2.');
  });

  it('explains an empty list', async () => {
    const { page, note } = await render([]);

    expect(page.querySelector('[data-testid="planning-rules-empty"]')?.textContent).toContain('No rules yet');
    expect(note()).toBe('Rules the agent reads: 0.');
  });

  it('switches a rule off and keeps it in the list', async () => {
    const { switches, settle, texts, note } = await render();

    switches()[0].click();
    await settle();

    expect(texts()).toHaveLength(3);
    expect(switches()[0].getAttribute('aria-checked')).toBe('false');
    expect(note()).toBe('Rules the agent reads: 1.');
    const save = http.expectOne(`${PLANNING_RULES_URL}/q/enabled`);
    expect(save.request.body).toEqual({ isEnabled: false });
    save.flush({ ...quick, isEnabled: false });
  });

  it('edits a rule when the field loses focus and ignores an empty text', async () => {
    const { page, settle } = await render();
    const input = page.querySelector<HTMLTextAreaElement>('[data-testid="planning-rule"] textarea')!;

    input.value = '  ';
    input.dispatchEvent(new Event('blur'));
    await settle();
    expect(input.value).toBe('Weekday dinners under 30 minutes');

    input.value = ' Weekday dinners under 20 minutes ';
    input.dispatchEvent(new Event('blur'));
    await settle();

    const save = http.expectOne(`${PLANNING_RULES_URL}/q/text`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ text: 'Weekday dinners under 20 minutes' });
    save.flush({ ...quick, text: 'Weekday dinners under 20 minutes' });
  });

  it('moves a rule up and saves the whole order', async () => {
    const { button, texts, settle } = await render();

    expect(button('Move rule 1 up').disabled).toBe(true);
    expect(button('Move rule 3 down').disabled).toBe(true);
    button('Move rule 3 up').click();
    await settle();

    expect(texts()).toEqual(['Weekday dinners under 30 minutes', 'Fish at least once a week', 'No deep frying']);
    const save = http.expectOne(`${PLANNING_RULES_URL}/order`);
    expect(save.request.body).toEqual({ planningRuleIds: ['q', 'h', 'f'] });
    save.flush(null);
  });

  it('removes a rule only after the confirmation', async () => {
    const { button, texts, settle } = await render();

    button('Remove rule 2').click();
    await settle();
    button('Cancel').click();
    await settle();
    expect(texts()).toHaveLength(3);

    button('Remove rule 2').click();
    await settle();
    button('Remove').click();
    await settle();

    expect(texts()).toEqual(['Weekday dinners under 30 minutes', 'Fish at least once a week']);
    const remove = http.expectOne(`${PLANNING_RULES_URL}/f`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });

  it('adds a rule at the end', async () => {
    const { page, settle, texts } = await render();
    const input = page.querySelector<HTMLInputElement>('form input')!;

    input.value = ' Sweet breakfast only at the weekend ';
    input.dispatchEvent(new Event('input'));
    page.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle();

    const add = http.expectOne({ method: 'POST', url: PLANNING_RULES_URL });
    expect(add.request.body).toEqual({ text: 'Sweet breakfast only at the weekend' });
    add.flush({ id: 'n', text: 'Sweet breakfast only at the weekend', position: 3, isEnabled: true });
    await settle();
    expect(texts()).toHaveLength(4);
    expect(input.value).toBe('');
  });

  it('reports a failed save and reloads the rules', async () => {
    const { page, switches, settle, texts } = await render();

    switches()[0].click();
    await settle();
    http.expectOne(`${PLANNING_RULES_URL}/q/enabled`).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();

    http.expectOne(PLANNING_RULES_URL).flush([quick, frying, fish]);
    await settle();
    expect(texts()).toHaveLength(3);
    expect(switches()[0].getAttribute('aria-checked')).toBe('true');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The change was not saved');
  });
});
