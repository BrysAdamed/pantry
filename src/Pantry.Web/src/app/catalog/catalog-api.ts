import { HttpClient, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Unit } from './units';

export const INGREDIENTS_URL = '/api/catalog/ingredients';
export const INGREDIENT_RESOLVE_URL = `${INGREDIENTS_URL}/resolve`;
export const INGREDIENT_NAME_MAX_LENGTH = 100;

export type IngredientCategory =
  | 'vegetablesAndFruit'
  | 'dairyAndEggs'
  | 'meatAndFish'
  | 'bakery'
  | 'dryAndCanned'
  | 'spices'
  | 'frozen'
  | 'drinks'
  | 'other';

export interface UnitConversion {
  unit: Unit;
  amount: number;
}

export interface Ingredient {
  id: string;
  name: string;
  aliases: string[];
  baseUnit: Unit;
  category: IngredientCategory;
  isStaple: boolean;
  needsReview: boolean;
  conversions: UnitConversion[];
}

export interface IngredientsPage {
  items: Ingredient[];
  total: number;
  page: number;
  pageSize: number;
}

/** The fixed category order of the catalog and of shopping lists. */
export const INGREDIENT_CATEGORIES: readonly IngredientCategory[] = [
  'vegetablesAndFruit',
  'dairyAndEggs',
  'meatAndFish',
  'bakery',
  'dryAndCanned',
  'spices',
  'frozen',
  'drinks',
  'other',
];

export const DEFAULT_INGREDIENT_CATEGORY: IngredientCategory = 'other';
export const INGREDIENTS_MAX_PAGE_SIZE = 200;
export const CONVERSION_AMOUNT_MAX = 1_000_000;
export const CONVERSION_AMOUNT_DECIMALS = 4;

export interface IngredientFilter {
  search?: string;
  category?: IngredientCategory;
  staple?: boolean;
  needsReview?: boolean;
}

/** Everything a person edits on an ingredient; conversions are in `baseUnit`. */
export interface IngredientChange {
  name: string;
  aliases: string[];
  category: IngredientCategory;
  baseUnit: Unit;
  isStaple: boolean;
  conversions: UnitConversion[];
}

/** A name or alias of the change that another ingredient already has (409 on create and update). */
export interface NameConflict {
  name: string;
  ingredientId: string;
  ingredientName: string;
}

/** How many records of one module (`recipes`, `shopping`, `household`...) point at an ingredient. */
export interface ModuleReferenceCount {
  module: string;
  count: number;
}

export interface IngredientReferences {
  references: ModuleReferenceCount[];
}

export function ingredientUrl(id: string): string {
  return `${INGREDIENTS_URL}/${id}`;
}

export function ingredientReferencesUrl(id: string): string {
  return `${ingredientUrl(id)}/references`;
}

/** The `conflicts` of a 409 from create or update; empty for any other error. */
export function nameConflictsOf(error: unknown): NameConflict[] {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict && Array.isArray(error.error?.conflicts)
    ? error.error.conflicts
    : [];
}

/** The `references` of a 409 from delete (the ingredient is in use); null for any other error. */
export function referencesOf(error: unknown): ModuleReferenceCount[] | null {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict && Array.isArray(error.error?.references)
    ? error.error.references
    : null;
}

/**
 * What a person chose in the ingredient picker: a catalog ingredient, or a new name that becomes one (flagged as
 * needing review) when the host form saves.
 */
export type IngredientChoice =
  | { readonly kind: 'existing'; readonly ingredient: Ingredient }
  | { readonly kind: 'new'; readonly name: string };

/** Writes of the Catalog module. Reads use `httpResource`. */
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  /** The ingredient known by `name`, created and flagged as needing review when there is none. */
  resolveIngredient(name: string, unitUsed: Unit | null = null): Promise<Ingredient> {
    return firstValueFrom(this.http.post<Ingredient>(INGREDIENT_RESOLVE_URL, { name, unit: unitUsed }));
  }

  /** The ingredient of a picker choice; a new name goes through `resolveIngredient`. Call it when the host saves. */
  async ingredientOf(choice: IngredientChoice, unitUsed: Unit | null = null): Promise<Ingredient> {
    return choice.kind === 'existing' ? choice.ingredient : this.resolveIngredient(choice.name, unitUsed);
  }

  /** Every ingredient matching `filter`, read page by page in the API's order (name, or best match when searching). */
  async allIngredients(filter: IngredientFilter): Promise<Ingredient[]> {
    const items: Ingredient[] = [];
    for (let page = 1; ; page++) {
      const result = await firstValueFrom(
        this.http.get<IngredientsPage>(INGREDIENTS_URL, { params: filterParams(filter, page, INGREDIENTS_MAX_PAGE_SIZE) }),
      );
      items.push(...result.items);
      if (result.items.length === 0 || items.length >= result.total) {
        return items;
      }
    }
  }

  createIngredient(change: IngredientChange): Promise<Ingredient> {
    return firstValueFrom(this.http.post<Ingredient>(INGREDIENTS_URL, change));
  }

  updateIngredient(id: string, change: IngredientChange): Promise<Ingredient> {
    return firstValueFrom(this.http.put<Ingredient>(ingredientUrl(id), change));
  }

  /** Clears needs review. */
  confirmIngredient(id: string): Promise<Ingredient> {
    return firstValueFrom(this.http.post<Ingredient>(`${ingredientUrl(id)}/confirm`, null));
  }

  /** Deletes an unused ingredient; a 409 carries the references (`referencesOf`). */
  deleteIngredient(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(ingredientUrl(id)));
  }
}

/** Query parameters of `GET /api/catalog/ingredients`; filters left out are not sent. */
export function filterParams(filter: IngredientFilter, page: number, pageSize: number): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = { page, pageSize };
  if (filter.search) {
    params['search'] = filter.search;
  }
  if (filter.category) {
    params['category'] = filter.category;
  }
  if (filter.staple !== undefined) {
    params['staple'] = filter.staple;
  }
  if (filter.needsReview !== undefined) {
    params['needsReview'] = filter.needsReview;
  }
  return params;
}
