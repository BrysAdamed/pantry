import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CatalogApi, Ingredient, INGREDIENT_RESOLVE_URL } from './catalog-api';

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
});
