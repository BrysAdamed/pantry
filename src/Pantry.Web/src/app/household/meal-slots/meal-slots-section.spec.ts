import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { MEAL_SLOTS_URL, MealSlot } from '../household-api';
import { MealSlotsSection } from './meal-slots-section';

const breakfast: MealSlot = { id: 'b', name: 'Breakfast', position: 0, isEnabled: true };
const snack: MealSlot = { id: 's', name: 'Snack', position: 1, isEnabled: false };
const dinner: MealSlot = { id: 'd', name: 'Dinner', position: 2, isEnabled: true };

describe('MealSlotsSection', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MealSlotsSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(slots: MealSlot[] = [breakfast, snack, dinner]) {
    const fixture = TestBed.createComponent(MealSlotsSection);
    fixture.detectChanges();
    http.expectOne(MEAL_SLOTS_URL).flush(slots);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const button = (label: string) =>
      [...page.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label)!;
    const names = () => [...page.querySelectorAll<HTMLInputElement>('[data-testid="meal-slot"] input')].map((i) => i.value);
    const switches = () => [...page.querySelectorAll<HTMLButtonElement>('[role="switch"]')];
    const note = () => page.querySelector('[data-testid="meal-slots-note"]')?.textContent?.replace(/\s+/g, ' ').trim();
    return { page, settle, button, names, switches, note };
  }

  it('shows the slots in order with their switches and the number of meals in the plan', async () => {
    const { names, switches, note } = await render();

    expect(names()).toEqual(['Breakfast', 'Snack', 'Dinner']);
    expect(switches().map((s) => s.getAttribute('aria-checked'))).toEqual(['true', 'false', 'true']);
    expect(switches()[1].getAttribute('aria-label')).toBe('Snack in the plan');
    expect(note()).toBe('Meals a day in the plan: 2.');
  });

  it('moves a slot down at once and saves the whole order', async () => {
    const { button, names, settle } = await render();

    button('Move Breakfast down').click();
    await settle();

    expect(names()).toEqual(['Snack', 'Breakfast', 'Dinner']);
    const save = http.expectOne(`${MEAL_SLOTS_URL}/order`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ mealSlotIds: ['s', 'b', 'd'] });
    save.flush(null);
  });

  it('disables the arrows at the ends of the list', async () => {
    const { button } = await render();

    expect(button('Move Breakfast up').disabled).toBe(true);
    expect(button('Move Breakfast down').disabled).toBe(false);
    expect(button('Move Dinner down').disabled).toBe(true);
  });

  it('sends quick moves one after another', async () => {
    const { button, settle } = await render();

    button('Move Dinner up').click();
    button('Move Dinner up').click();
    await settle();

    http.expectOne(`${MEAL_SLOTS_URL}/order`).flush(null);
    await settle();
    expect(http.expectOne(`${MEAL_SLOTS_URL}/order`).request.body).toEqual({ mealSlotIds: ['d', 'b', 's'] });
  });

  it('renames a slot when the field loses focus and ignores an empty name', async () => {
    const { page, settle } = await render();
    const input = page.querySelector<HTMLInputElement>('[data-testid="meal-slot"] input')!;

    input.value = '  ';
    input.dispatchEvent(new Event('blur'));
    await settle();
    expect(input.value).toBe('Breakfast');

    input.value = ' Morning meal ';
    input.dispatchEvent(new Event('blur'));
    await settle();

    const save = http.expectOne(`${MEAL_SLOTS_URL}/b/name`);
    expect(save.request.body).toEqual({ name: 'Morning meal' });
    save.flush({ ...breakfast, name: 'Morning meal' });
  });

  it('switches a slot into the plan', async () => {
    const { switches, settle, note } = await render();

    switches()[1].click();
    await settle();

    expect(switches()[1].getAttribute('aria-checked')).toBe('true');
    expect(note()).toBe('Meals a day in the plan: 3.');
    const save = http.expectOne(`${MEAL_SLOTS_URL}/s/enabled`);
    expect(save.request.body).toEqual({ isEnabled: true });
    save.flush({ ...snack, isEnabled: true });
  });

  it('keeps the last enabled slot in the plan', async () => {
    const { switches, note } = await render([breakfast, snack]);

    expect(switches()[0].disabled).toBe(true);
    expect(switches()[1].disabled).toBe(false);
    expect(note()).toBe('Meals a day in the plan: 1. At least one meal stays in the plan.');
  });

  it('adds a slot at the end', async () => {
    const { page, settle, names } = await render();
    const input = page.querySelector<HTMLInputElement>('form input')!;

    input.value = ' Dinner out ';
    input.dispatchEvent(new Event('input'));
    page.querySelector('form')!.dispatchEvent(new Event('submit'));
    await settle();

    const add = http.expectOne({ method: 'POST', url: MEAL_SLOTS_URL });
    expect(add.request.body).toEqual({ name: 'Dinner out' });
    add.flush({ id: 'o', name: 'Dinner out', position: 3, isEnabled: true });
    await settle();
    expect(names()).toEqual(['Breakfast', 'Snack', 'Dinner', 'Dinner out']);
    expect(input.value).toBe('');
  });

  it('reports a failed save and reloads the slots', async () => {
    const { page, button, settle, names } = await render();

    button('Move Breakfast down').click();
    await settle();
    http.expectOne(`${MEAL_SLOTS_URL}/order`).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();

    http.expectOne(MEAL_SLOTS_URL).flush([breakfast, snack, dinner]);
    await settle();
    expect(names()).toEqual(['Breakfast', 'Snack', 'Dinner']);
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('The change was not saved');
  });
});
