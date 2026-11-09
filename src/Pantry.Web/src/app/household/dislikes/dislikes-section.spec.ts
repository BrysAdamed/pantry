import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Ingredient, INGREDIENT_RESOLVE_URL, INGREDIENTS_URL } from '../../catalog/catalog-api';
import { INGREDIENT_PICKER_RESULTS, INGREDIENT_SEARCH_DELAY_MS } from '../../catalog/ingredient-picker/ingredient-picker';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { Dislike, DISLIKES_URL } from '../household-api';
import { DislikesSection } from './dislikes-section';

function ingredient(id: string, name: string): Ingredient {
  return { id, name, aliases: [], baseUnit: 'g', category: 'other', isStaple: false, needsReview: false, conversions: [] };
}

const walnuts: Dislike = { id: 'd1', ingredientId: 'walnuts', ingredientName: 'Walnuts', kind: 'never', reason: 'allergy' };
const mushrooms: Dislike = { id: 'd2', ingredientId: 'mushrooms', ingredientName: 'Wild mushrooms', kind: 'never', reason: null };
const coriander: Dislike = { id: 'd3', ingredientId: 'coriander', ingredientName: 'Coriander', kind: 'avoid', reason: null };

describe('DislikesSection', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DislikesSection],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslocoTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(dislikes: Dislike[] = [walnuts, mushrooms, coriander]) {
    const fixture = TestBed.createComponent(DislikesSection);
    fixture.detectChanges();
    http.expectOne(DISLIKES_URL).flush(dislikes);
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      await fixture.whenStable();
    };
    const column = (kind: 'never' | 'avoid') => page.querySelector<HTMLElement>(`[data-testid="dislikes-${kind}"]`)!;
    const chips = (kind: 'never' | 'avoid') =>
      [...column(kind).querySelectorAll('[data-testid="dislike"]')].map((c) => c.textContent!.replace(/\s+/g, ' ').trim());
    const button = (scope: HTMLElement, label: string) =>
      [...scope.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label)!;
    const search = async (kind: 'never' | 'avoid', text: string, items: Ingredient[]) => {
      const field = column(kind).querySelector<HTMLInputElement>('input[role="combobox"]')!;
      field.focus();
      field.value = text;
      field.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, INGREDIENT_SEARCH_DELAY_MS + 20));
      TestBed.tick();
      http.expectOne((r) => r.url === INGREDIENTS_URL).flush({ items, total: items.length, page: 1, pageSize: INGREDIENT_PICKER_RESULTS });
      await fixture.whenStable();
      return [...column(kind).querySelectorAll<HTMLElement>('[role="option"]')];
    };
    const submit = async (kind: 'never' | 'avoid') => {
      column(kind).querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      await settle();
    };
    return { page, settle, column, chips, button, search, submit };
  }

  it('shows never and avoid in two columns with reasons', async () => {
    const { chips, column } = await render();

    expect(chips('never')).toEqual(['Walnuts · allergy', 'Wild mushrooms']);
    expect(chips('avoid')).toEqual(['Coriander']);
    expect(column('never').querySelector('h3')?.textContent?.trim()).toBe('Never');
    expect(column('avoid').querySelector('h3')?.textContent?.trim()).toBe('Avoid');
  });

  it('explains empty columns', async () => {
    const { page } = await render([]);

    expect([...page.querySelectorAll('[data-testid="dislikes-empty"]')].map((p) => p.textContent?.trim())).toEqual([
      'Nothing here yet.',
      'Nothing here yet.',
    ]);
  });

  it('adds a catalog ingredient to never with a reason', async () => {
    const { column, chips, search, submit, settle } = await render([coriander]);

    const [option] = await search('never', 'pean', [ingredient('peanuts', 'Peanuts')]);
    option.click();
    await settle();
    expect(column('never').querySelector('[data-testid="dislike-chosen"]')?.textContent?.trim()).toBe('Peanuts');
    const reason = column('never').querySelector<HTMLInputElement>('input[aria-label="Reason"]')!;
    reason.value = ' allergy ';
    reason.dispatchEvent(new Event('input'));
    await submit('never');

    const save = http.expectOne(DISLIKES_URL);
    expect(save.request.method).toBe('POST');
    expect(save.request.body).toEqual({ ingredientId: 'peanuts', kind: 'never', reason: 'allergy' });
    save.flush({ id: 'd9', ingredientId: 'peanuts', ingredientName: 'Peanuts', kind: 'never', reason: 'allergy' });
    await settle();
    expect(chips('never')).toEqual(['Peanuts · allergy']);
    expect(column('never').querySelector('[data-testid="dislike-chosen"]')).toBeNull();
    expect(reason.value).toBe('');
  });

  it('adds a new name to avoid after resolving it in the catalog', async () => {
    const { column, chips, search, submit, settle } = await render([]);

    const options = await search('avoid', 'Okra', []);
    options.at(-1)!.click();
    await settle();
    expect(column('avoid').querySelector('[data-testid="dislike-chosen"]')?.textContent).toContain('new');
    await submit('avoid');

    const resolve = http.expectOne(INGREDIENT_RESOLVE_URL);
    expect(resolve.request.body).toEqual({ name: 'Okra', unit: null });
    resolve.flush(ingredient('okra', 'Okra'));
    await settle();
    const save = http.expectOne(DISLIKES_URL);
    expect(save.request.body).toEqual({ ingredientId: 'okra', kind: 'avoid', reason: null });
    save.flush({ id: 'd9', ingredientId: 'okra', ingredientName: 'Okra', kind: 'avoid', reason: null });
    await settle();
    expect(chips('avoid')).toEqual(['Okra']);
  });

  it('shows disliked ingredients as taken in the picker', async () => {
    const { search } = await render();

    const [option] = await search('avoid', 'walnut', [ingredient('walnuts', 'Walnuts')]);

    expect(option.getAttribute('aria-disabled')).toBe('true');
  });

  it('moves a dislike to the other column and drops the reason', async () => {
    const { column, chips, button, settle } = await render();

    button(column('never'), 'Move Walnuts to "avoid"').click();
    await settle();

    expect(chips('never')).toEqual(['Wild mushrooms']);
    expect(chips('avoid')).toEqual(['Coriander', 'Walnuts']);
    const save = http.expectOne(`${DISLIKES_URL}/d1`);
    expect(save.request.method).toBe('PUT');
    expect(save.request.body).toEqual({ kind: 'avoid', reason: null });
    save.flush({ ...walnuts, kind: 'avoid', reason: null });
  });

  it('removes a dislike', async () => {
    const { column, chips, button, settle } = await render();

    button(column('avoid'), 'Remove Coriander').click();
    await settle();

    expect(chips('avoid')).toEqual([]);
    const remove = http.expectOne(`${DISLIKES_URL}/d3`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('reloads the list when the ingredient was disliked in the meantime', async () => {
    const { page, column, chips, search, submit, settle } = await render([]);

    const [option] = await search('never', 'walnut', [ingredient('walnuts', 'Walnuts')]);
    option.click();
    await settle();
    await submit('never');
    http.expectOne(DISLIKES_URL).flush({ dislikeId: 'd1' }, { status: 409, statusText: 'Conflict' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    http.expectOne(DISLIKES_URL).flush([walnuts]);
    await settle();

    expect(page.querySelector('[role="status"]')?.textContent).toContain('already on the list');
    expect(column('never').querySelector('[data-testid="dislike-chosen"]')).toBeNull();
    expect(chips('never')).toEqual(['Walnuts · allergy']);
  });

  it('reports a failed change and reloads the list', async () => {
    const { page, column, button, settle, chips } = await render();

    button(column('avoid'), 'Remove Coriander').click();
    await settle();
    http.expectOne(`${DISLIKES_URL}/d3`).flush(null, { status: 500, statusText: 'Server Error' });
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    http.expectOne(DISLIKES_URL).flush([walnuts, mushrooms, coriander]);
    await settle();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('was not saved');
    expect(chips('avoid')).toEqual(['Coriander']);
  });
});
