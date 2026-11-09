import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CatalogApi, Ingredient, INGREDIENT_RESOLVE_URL, INGREDIENTS_MAX_PAGE_SIZE, INGREDIENTS_URL } from './catalog-api';

const garlic: Ingredient = {
  id: 'garlic',
  name: 'Czosnek',
  aliases: [],
  baseUnit: 'piece',
  category: 'other',
  isStaple: false,
  needsReview: true,
  conversions: [],
};

describe('CatalogApi', () => {
  let api: CatalogApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(CatalogApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('resolves a new name from the picker with the unit it was used with', async () => {
    const resolved = api.ingredientOf({ kind: 'new', name: 'Czosnek' }, 'clove');

    const request = http.expectOne(INGREDIENT_RESOLVE_URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Czosnek', unit: 'clove' });
    request.flush(garlic, { status: 201, statusText: 'Created' });

    expect(await resolved).toEqual(garlic);
  });

  it('keeps an existing ingredient without a call', async () => {
    expect(await api.ingredientOf({ kind: 'existing', ingredient: garlic })).toBe(garlic);
  });
  it('reads every page of a filtered catalog', async () => {
    const all = api.allIngredients({ search: 'cz', needsReview: true });
    const onion = { ...garlic, id: 'onion' };

    const first = http.expectOne((r) => r.url === INGREDIENTS_URL && r.params.get('page') === '1');
    expect(first.request.params.get('search')).toBe('cz');
    expect(first.request.params.get('needsReview')).toBe('true');
    expect(first.request.params.has('staple')).toBe(false);
    expect(first.request.params.get('pageSize')).toBe(String(INGREDIENTS_MAX_PAGE_SIZE));
    first.flush({ items: [garlic], total: 2, page: 1, pageSize: 1 });
    await new Promise((resolve) => setTimeout(resolve));
    http.expectOne((r) => r.params.get('page') === '2').flush({ items: [onion], total: 2, page: 2, pageSize: 1 });

    expect(await all).toEqual([garlic, onion]);
  });
});
