import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { RecipeListItem } from '../recipes-api';
import { recipesRoutes } from '../recipes.routes';
import { localDateIso as daysFromToday } from '../local-date';
import { RECIPE_LIBRARY_PAGE_SIZE, RECIPE_LIBRARY_SEARCH_DELAY_MS } from './recipe-library-screen';

function recipe(id: string, name: string, change: Partial<RecipeListItem> = {}): RecipeListItem {
  return {
    id,
    name,
    servings: 2,
    prepTimeMinutes: null,
    tags: [],
    nutrition: { kcal: null, protein: null, fat: null, carbs: null },
    makeAgain: true,
    averageRating: null,
    ratingCount: 0,
    isFavourite: false,
    cooldownDays: null,
    cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom: null },
    isArchived: false,
    updatedAt: '2026-11-01T10:00:00Z',
    dislikes: [],
    ...change,
  };
}

describe('Recipe library', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let library: RecipeListItem[];
  let requests: URLSearchParams[];

  beforeEach(() => {
    requests = [];
    library = [
      recipe('curry', 'Chicken curry', {
        prepTimeMinutes: 40,
        tags: ['dinner'],
        averageRating: 4.5,
        ratingCount: 2,
        nutrition: { kcal: 640, protein: null, fat: null, carbs: null },
        cooldown: { days: 21, isHouseholdDefault: true, lastEaten: daysFromToday(-1), availableFrom: daysFromToday(20) },
        dislikes: [
          { ingredientId: 'peanuts', ingredientName: 'peanuts', kind: 'never', reason: 'allergy' },
          { ingredientId: 'coriander', ingredientName: 'coriander', kind: 'avoid', reason: null },
        ],
      }),
      recipe('salmon', 'Salmon with potatoes', { prepTimeMinutes: 75, tags: ['dinner', 'fish'], makeAgain: false }),
      recipe('porridge', 'Porridge', { prepTimeMinutes: 10, tags: ['breakfast'] }),
    ];
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        provideRouter([{ path: 'recipes', children: recipesRoutes }], withComponentInputBinding()),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

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
    const url = new URL(request.request.urlWithParams, 'http://x');
    if (url.pathname === '/api/recipes/history/to-rate' || url.pathname === '/api/household/meal-slots') {
      request.flush([]);
      return;
    }
    if (url.pathname === '/api/recipes/tags') {
      request.flush([
        { tag: 'dinner', recipes: 2 },
        { tag: 'breakfast', recipes: 1 },
      ]);
      return;
    }
    const params = url.searchParams;
    const pageSize = Number(params.get('pageSize'));
    if (pageSize !== 1) {
      requests.push(params);
    }
    const search = params.get('search')?.toLowerCase();
    const tags = params.getAll('tag');
    const items = library
      .filter((r) => r.isArchived === (params.get('archived') === 'true'))
      .filter((r) => !search || r.name.toLowerCase().includes(search))
      .filter((r) => tags.every((tag) => r.tags.includes(tag)))
      .filter((r) => params.get('withoutNever') !== 'true' || !r.dislikes.some((d) => d.kind === 'never'));
    const page = Number(params.get('page'));
    request.flush({ items: items.slice((page - 1) * pageSize, page * pageSize), total: items.length, page, pageSize });
  }

  async function open(url = '/recipes') {
    harness = await RouterTestingHarness.create();
    void harness.navigateByUrl(url);
    await settle();
    return page();
  }

  const page = () => harness.fixture.nativeElement as HTMLElement;
  const texts = (testId: string, root: ParentNode = page()) =>
    [...root.querySelectorAll(`[data-testid="${testId}"]`)].map((e) => e.textContent?.trim().replace(/\s+/g, ' '));
  const card = (name: string) =>
    [...page().querySelectorAll<HTMLElement>('[data-testid="recipe-card"]')].find((c) => c.querySelector('[data-testid="recipe-name"]')?.textContent?.trim() === name)!;
  const click = (testId: string, text?: string) =>
    [...page().querySelectorAll<HTMLElement>(`[data-testid="${testId}"]`)].find((e) => !text || e.textContent?.trim() === text)!.click();
  const lastRequest = () => requests[requests.length - 1];

  it('lists recipes by rating with their facts and badges', async () => {
    await open();

    expect(texts('recipe-name')).toEqual(['Chicken curry', 'Salmon with potatoes', 'Porridge']);
    expect(lastRequest().get('sort')).toBe('rating');
    expect(texts('recipe-count')).toEqual(['3 recipes']);
    const curry = card('Chicken curry');
    expect(curry.getAttribute('href')).toBe('/recipes/curry');
    expect(texts('recipe-tile', curry)).toEqual(['C']);
    expect(curry.querySelector('[data-testid="recipe-rating"]')?.getAttribute('aria-label')).toBe('Rated 4.5 out of 5');
    expect(texts('recipe-time', curry)).toEqual(['40 min']);
    expect(texts('recipe-last-eaten', curry)).toEqual(['yesterday']);
    expect(texts('recipe-kcal', curry)).toEqual(['640 kcal']);
    expect(texts('badge-never', curry)).toEqual(['never']);
    expect(texts('badge-avoid', curry)).toEqual(['avoid']);
    expect(texts('badge-too-soon', curry)).toEqual(['too soon']);
    expect(texts('badge-not-again', curry)).toEqual([]);

    const salmon = card('Salmon with potatoes');
    expect(texts('recipe-rating', salmon)).toEqual(['not rated']);
    expect(texts('recipe-time', salmon)).toEqual(['1 h 15 min']);
    expect(texts('recipe-last-eaten', salmon)).toEqual(['not eaten yet']);
    expect(texts('badge-not-again', salmon)).toEqual(['not cooking again']);
    expect(texts('badge-too-soon', salmon)).toEqual([]);
  });

  it('searches after a pause', async () => {
    await open();

    const input = page().querySelector<HTMLInputElement>('[data-testid="recipe-search"]')!;
    input.value = 'curr';
    input.dispatchEvent(new Event('input'));
    await vi.waitFor(
      async () => {
        await settle();
        expect(texts('recipe-name')).toEqual(['Chicken curry']);
      },
      { timeout: RECIPE_LIBRARY_SEARCH_DELAY_MS * 20, interval: RECIPE_LIBRARY_SEARCH_DELAY_MS },
    );
    expect(lastRequest().get('search')).toBe('curr');
    expect(texts('recipe-count')).toEqual(['1 recipe']);
  });

  it('filters by tags, without never and the archive, and sorts', async () => {
    library.push(recipe('soup', 'Old soup', { isArchived: true }));
    await open();

    expect(texts('filter-tag')).toEqual(['dinner', 'breakfast']);
    click('filter-tag', 'dinner');
    await settle();
    expect(lastRequest().getAll('tag')).toEqual(['dinner']);
    expect(texts('recipe-name')).toEqual(['Chicken curry', 'Salmon with potatoes']);

    click('filter-without-never');
    await settle();
    expect(lastRequest().get('withoutNever')).toBe('true');
    expect(texts('recipe-name')).toEqual(['Salmon with potatoes']);

    const sort = page().querySelector<HTMLSelectElement>('[data-testid="recipe-sort"]')!;
    sort.value = 'notEatenLately';
    sort.dispatchEvent(new Event('change'));
    await settle();
    expect(lastRequest().get('sort')).toBe('notEatenLately');

    click('filter-tag', 'dinner');
    click('filter-without-never');
    click('filter-archived');
    await settle();
    expect(lastRequest().get('archived')).toBe('true');
    expect(texts('recipe-name')).toEqual(['Old soup']);
    expect(texts('recipe-count')).toEqual(['1 archived recipe']);
  });

  it('says when nothing matches and clears the filters', async () => {
    await open();

    click('filter-tag', 'breakfast');
    click('filter-without-never');
    await settle();
    click('filter-tag', 'dinner');
    await settle();

    expect(texts('recipe-name')).toEqual([]);
    expect(page().querySelector('[data-testid="no-results"] h2')?.textContent).toBe('Nothing matches');
    page().querySelector<HTMLButtonElement>('[data-testid="no-results"] button')!.click();
    await settle();
    expect(texts('recipe-name')).toHaveLength(3);
    expect(page().querySelectorAll('[data-testid="filter-tag"][aria-pressed="true"]')).toHaveLength(0);
  });

  it('loads more recipes on request', async () => {
    library = Array.from({ length: RECIPE_LIBRARY_PAGE_SIZE + 2 }, (_, i) => recipe(`r${i}`, `Recipe ${String(i).padStart(2, '0')}`));
    await open();

    expect(texts('recipe-name')).toHaveLength(RECIPE_LIBRARY_PAGE_SIZE);
    click('show-more');
    await settle();

    expect(lastRequest().get('page')).toBe('2');
    expect(texts('recipe-name')).toHaveLength(RECIPE_LIBRARY_PAGE_SIZE + 2);
    expect(page().querySelector('[data-testid="show-more"]')).toBeNull();
  });

  it('welcomes an empty library with a link to the editor', async () => {
    library = [];
    await open();

    expect(page().querySelector('[data-testid="library-empty"] h2')?.textContent).toBe('Add your first recipe');
    expect(page().querySelector('[data-testid="recipe-search"]')).toBeNull();
    expect(page().querySelector('[data-testid="new-recipe"]')?.getAttribute('href')).toBe('/recipes/new');
  });
});
