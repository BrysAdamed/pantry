import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Ingredient, ModuleReferenceCount } from '../catalog/catalog-api';
import { Toasts } from '../core/toast/toasts';
import { provideTranslocoTesting } from '../testing/transloco-testing';
import { INGREDIENT_LIST_SEARCH_DELAY_MS } from './ingredients-screen';
import { ingredientsRoutes } from './ingredients.routes';

function ingredient(id: string, name: string, change: Partial<Ingredient> = {}): Ingredient {
  return { id, name, aliases: [], baseUnit: 'g', category: 'vegetablesAndFruit', isStaple: false, needsReview: false, conversions: [], ...change };
}

describe('Ingredients screen', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let catalog: Ingredient[];
  let references: Record<string, ModuleReferenceCount[]>;
  let toasts: string[];

  beforeEach(() => {
    catalog = [
      ingredient('onion', 'onion', { aliases: ['shallot'], conversions: [{ unit: 'piece', amount: 150 }] }),
      ingredient('garlic', 'garlic', { needsReview: true }),
      ingredient('milk', 'milk', { baseUnit: 'ml', category: 'dairyAndEggs' }),
      ingredient('salt', 'salt', { category: 'spices', isStaple: true }),
    ];
    references = {};
    toasts = [];
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        provideRouter([{ path: 'ingredients', children: ingredientsRoutes }], withComponentInputBinding()),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const show = TestBed.inject(Toasts).show.bind(TestBed.inject(Toasts));
    vi.spyOn(TestBed.inject(Toasts), 'show').mockImplementation((message) => {
      toasts.push(message);
      show(message);
    });
  });

  afterEach(() => http.verify());

  /** Answers every pending request from the in-memory catalog until none is left. */
  async function settle(): Promise<void> {
    for (let round = 0; round < 10; round++) {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      const pending = http.match(() => true);
      if (!pending.length) {
        await harness.fixture.whenStable();
        return;
      }
      pending.forEach(answer);
    }
  }

  function answer(request: TestRequest): void {
    const { method, params, urlWithParams } = request.request;
    const [id, action] = new URL(urlWithParams, 'http://x').pathname.split('/').slice(4);
    if (!id) {
      const search = params.get('search')?.toLowerCase();
      const items = catalog
        .filter((i) => !search || [i.name, ...i.aliases].some((n) => n.includes(search)))
        .filter((i) => !params.has('needsReview') || String(i.needsReview) === params.get('needsReview'))
        .filter((i) => !params.has('staple') || String(i.isStaple) === params.get('staple'))
        .filter((i) => !params.has('category') || i.category === params.get('category'))
        .sort((a, b) => a.name.localeCompare(b.name));
      const pageSize = Number(params.get('pageSize'));
      request.flush({ items: items.slice(0, pageSize), total: items.length, page: 1, pageSize });
      return;
    }
    const found = catalog.find((i) => i.id === id);
    if (action === 'references') {
      request.flush({ references: references[id] ?? [] });
    } else if (action === 'confirm') {
      found!.needsReview = false;
      request.flush({ ...found! });
    } else if (method === 'PUT') {
      Object.assign(found!, request.request.body);
      request.flush({ ...found! });
    } else if (method === 'DELETE') {
      if (references[id]?.length) {
        request.flush({ references: references[id] }, { status: 409, statusText: 'Conflict' });
      } else {
        catalog = catalog.filter((i) => i.id !== id);
        request.flush(null, { status: 204, statusText: 'No Content' });
      }
    } else {
      request.flush(found ? { ...found } : null, found ? {} : { status: 404, statusText: 'Not Found' });
    }
  }

  /** Lets signals and the dialog render without waiting for the app to be stable (a guard may be waiting). */
  async function render(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  async function open(url: string) {
    harness = await RouterTestingHarness.create();
    void harness.navigateByUrl(url);
    await settle();
    return harness.fixture.nativeElement as HTMLElement;
  }

  const page = () => harness.fixture.nativeElement as HTMLElement;
  const texts = (testId: string) => [...page().querySelectorAll(`[data-testid="${testId}"]`)].map((e) => e.textContent?.trim());
  const button = (label: string, root: ParentNode = page()) =>
    [...root.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.getAttribute('aria-label') ?? b.textContent?.trim()) === label)!;
  const dialog = () => document.querySelector<HTMLElement>('[role="dialog"]');
  const type = (input: HTMLInputElement | null, value: string) => {
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
  };

  it('groups the catalog by category in the shopping list order', async () => {
    await open('/ingredients');

    expect([...page().querySelectorAll('[data-testid="ingredient-group"] h3')].map((h) => h.firstElementChild?.textContent)).toEqual([
      'Vegetables and fruit',
      'Dairy and eggs',
      'Spices',
    ]);
    expect(texts('ingredient-name')).toEqual(['garlic', 'onion', 'milk', 'salt']);
    expect(texts('ingredient-count')).toEqual(['4 ingredients']);
    expect(texts('tidy-up')[0]).toContain('1 ingredient to review');
  });

  it('searches names and aliases and says which alias matched', async () => {
    await open('/ingredients');

    type(page().querySelector('input[type="search"]'), 'shal');
    await new Promise((resolve) => setTimeout(resolve, INGREDIENT_LIST_SEARCH_DELAY_MS + 10));
    await settle();

    expect(texts('ingredient-name')).toEqual(['onion']);
    expect(texts('alias-match')).toEqual(['matches: shallot']);
    expect(page().querySelector('[data-testid="alias-match"] mark')?.textContent).toBe('shal');
    expect(texts('ingredient-count')).toEqual(['1 ingredient (of 4)']);
  });

  it('filters by needs review and shows the empty queue when all are reviewed', async () => {
    catalog[1].needsReview = false;
    await open('/ingredients');

    expect(page().querySelector('[data-testid="tidy-up"]')).toBeNull();
    page().querySelector<HTMLButtonElement>('[data-testid="filter-needs-review"]')!.click();
    await settle();

    expect(texts('ingredient-name')).toEqual([]);
    expect(texts('review-done')[0]).toContain('All reviewed');
  });

  it('welcomes an empty catalog', async () => {
    catalog = [];
    await open('/ingredients');

    expect(page().textContent).toContain('Ingredients show up on their own');
    expect(page().querySelector('[data-testid="tidy-up"]')).toBeNull();
  });

  it('saves the changed form and shows the new name', async () => {
    await open('/ingredients/onion');

    type(page().querySelector('[data-testid="ingredient-name-input"]'), 'red onion');
    await settle();
    expect(texts('unsaved')).toEqual(['Unsaved changes']);
    button('Save').click();
    await settle();

    expect(catalog[0]).toMatchObject({ name: 'red onion', aliases: ['shallot'], baseUnit: 'g', conversions: [{ unit: 'piece', amount: 150 }] });
    expect(texts('ingredient-title')).toEqual(['red onion']);
    expect(texts('unsaved')).toEqual([]);
    expect(toasts).toEqual(['Ingredient saved']);
  });

  it('shows which name another ingredient already has', async () => {
    await open('/ingredients/garlic');

    type(page().querySelector('[data-testid="ingredient-name-input"]'), 'Onion');
    await settle();
    button('Save').click();
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne('/api/catalog/ingredients/garlic').flush(
      { conflicts: [{ name: 'Onion', ingredientId: 'onion', ingredientName: 'onion' }] },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();

    expect(texts('name-error')).toEqual(['This name or alias already exists: "onion".']);
    expect(page().querySelector('[data-testid="ingredient-name-input"]')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('asks before a new base unit removes the conversions', async () => {
    await open('/ingredients/onion');
    const millilitres = () => [...page().querySelectorAll<HTMLButtonElement>('[role="radio"]')][1];

    millilitres().click();
    await settle();
    expect(dialog()?.textContent).toContain('Conversions are stored in g and will be removed: 1 pc = 150 g.');
    button('Cancel', dialog()!).click();
    await settle();
    expect(millilitres().getAttribute('aria-checked')).toBe('false');

    millilitres().click();
    await settle();
    button('Change unit', dialog()!).click();
    await settle();

    expect(millilitres().getAttribute('aria-checked')).toBe('true');
    expect(texts('conversion')).toEqual([]);
  });

  it('adds a conversion and explains a wrong amount', async () => {
    await open('/ingredients/garlic');
    const unit = page().querySelector<HTMLSelectElement>('[data-testid="conversion-unit"]')!;

    button('Add').click();
    await settle();
    expect(texts('conversion-error')).toEqual(['Pick a unit.']);

    unit.value = 'clove';
    unit.dispatchEvent(new Event('change'));
    type(page().querySelector('[data-testid="conversion-amount"]'), '5');
    button('Add').click();
    await settle();

    expect(texts('conversion')).toEqual(['1 clove = 5 g']);
    expect([...unit.options].map((o) => o.value)).not.toContain('clove');
  });

  it('confirms an ingredient that needs review', async () => {
    await open('/ingredients/garlic');

    button('Confirm', page().querySelector('[data-testid="review-banner"]')!).click();
    await settle();

    expect(catalog[1].needsReview).toBe(false);
    expect(page().querySelector('[data-testid="review-banner"]')).toBeNull();
    expect(toasts).toEqual(['Confirmed']);
  });

  it('deletes an unused ingredient after asking', async () => {
    await open('/ingredients/salt');

    button('Delete').click();
    await settle();
    expect(dialog()?.textContent).toContain('It is not used anywhere. This cannot be undone.');
    button('Delete', dialog()!).click();
    await settle();

    expect(catalog.map((i) => i.id)).not.toContain('salt');
    expect(TestBed.inject(Router).url).toBe('/ingredients');
    expect(toasts).toEqual(['Ingredient deleted']);
  });

  it('keeps a used ingredient and says where it is used', async () => {
    references['milk'] = [
      { module: 'recipes', count: 3 },
      { module: 'shopping', count: 1 },
    ];
    await open('/ingredients/milk');

    expect(page().querySelector('[data-testid="usage"]')?.textContent).toContain('Recipes');
    button('Delete').click();
    await settle();

    expect(dialog()?.querySelector('[data-testid="in-use"]')?.textContent).toContain('3 recipes, 1 shopping list.');
    expect(catalog.map((i) => i.id)).toContain('milk');
    button('Close', dialog()!).click();
    await settle();
  });

  it('asks before leaving unsaved changes', async () => {
    await open('/ingredients/onion');
    type(page().querySelector('[data-testid="ingredient-name-input"]'), 'red onion');
    await settle();

    const leaving = TestBed.inject(Router).navigateByUrl('/ingredients/milk');
    await render();
    expect(dialog()?.textContent).toContain('Changes to "onion" are not saved.');
    button('Keep editing', dialog()!).click();
    expect(await leaving).toBe(false);

    const discarding = TestBed.inject(Router).navigateByUrl('/ingredients/milk');
    await render();
    button('Discard', dialog()!).click();
    expect(await discarding).toBe(true);
    await settle();
    expect(texts('ingredient-title')).toEqual(['milk']);
  });
});
