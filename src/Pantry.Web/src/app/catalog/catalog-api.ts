import { HttpClient } from '@angular/common/http';
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
}
