import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Unit } from '../catalog/units';
import { DislikeKind } from '../household/household-api';

export const RECIPES_URL = '/api/recipes';
export const RECIPE_TAGS_URL = `${RECIPES_URL}/tags`;

/** The limits of the API's `RecipeContent`, so the editor can say what is wrong before saving. */
export const RECIPE_LIMITS = {
  nameMaxLength: 200,
  maxServings: 100,
  maxPrepTimeMinutes: 10_080,
  sourceUrlMaxLength: 2000,
  maxCooldownDays: 365,
  maxTags: 20,
  tagMaxLength: 40,
  maxSteps: 100,
  stepMaxLength: 4000,
  maxLines: 100,
  sectionNameMaxLength: 100,
  noteMaxLength: 200,
  maxQuantity: 100_000,
  quantityDecimals: 3,
  maxNutrition: 100_000,
} as const;

export function recipeUrl(id: string): string {
  return `${RECIPES_URL}/${id}`;
}

/** Per one serving; null when the source does not say. */
export interface Nutrition {
  kcal: number | null;
  protein: number | null;
  fat: number | null;
  carbs: number | null;
}

/** An ingredient line; `quantity` and `unit` are both null for an ingredient added to taste. */
export interface RecipeLine {
  ingredientId: string;
  /** Null only when the ingredient no longer exists in the catalog. */
  ingredientName: string | null;
  ingredientNeedsReview: boolean;
  /** As entered for the recipe servings. */
  quantity: number | null;
  quantityPerServing: number | null;
  unit: Unit | null;
  note: string | null;
}

export interface RecipeSection {
  name: string;
  lines: RecipeLine[];
}

/** A household dislike among a recipe's ingredients, never first. */
export interface RecipeDislike {
  ingredientId: string;
  ingredientName: string | null;
  kind: DislikeKind;
  reason: string | null;
}

/** A recipe as the detail and the editor read it: `lines` outside any section first, then `sections` in order. */
export interface Recipe {
  id: string;
  name: string;
  servings: number;
  prepTimeMinutes: number | null;
  sourceUrl: string | null;
  tags: string[];
  steps: string[];
  nutrition: Nutrition;
  makeAgain: boolean;
  /** The recipe's own cooldown; null uses the household default. */
  cooldownDays: number | null;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  lines: RecipeLine[];
  sections: RecipeSection[];
  dislikes: RecipeDislike[];
}

/** A line to save: a catalog ingredient by id, or a name the API resolves (a new one joins the catalog needing review). */
export interface RecipeLineChange {
  ingredientId?: string;
  ingredientName?: string;
  quantity: number | null;
  unit: Unit | null;
  note: string | null;
}

export interface RecipeSectionChange {
  name: string;
  lines: RecipeLineChange[];
}

/** Everything a person edits on a recipe, for create and update. Quantities are for `servings`. */
export interface RecipeChange {
  name: string;
  servings: number;
  prepTimeMinutes: number | null;
  sourceUrl: string | null;
  tags: string[];
  steps: string[];
  nutrition: Nutrition;
  makeAgain: boolean;
  cooldownDays: number | null;
  lines: RecipeLineChange[];
  sections: RecipeSectionChange[];
}

/**
 * The cooldown in effect: the recipe's own `days` or the household default. `availableFrom` (last eaten + days) is null
 * when the recipe was never eaten, which means it is available now; planning it before that date is *too soon*.
 */
export interface RecipeCooldown {
  days: number;
  isHouseholdDefault: boolean;
  lastEaten: string | null;
  availableFrom: string | null;
}

/** A recipe as the library lists it. `averageRating` is null while no history entry is rated. */
export interface RecipeListItem {
  id: string;
  name: string;
  servings: number;
  prepTimeMinutes: number | null;
  tags: string[];
  nutrition: Nutrition;
  makeAgain: boolean;
  averageRating: number | null;
  ratingCount: number;
  isFavourite: boolean;
  cooldownDays: number | null;
  cooldown: RecipeCooldown;
  isArchived: boolean;
  updatedAt: string;
  dislikes: RecipeDislike[];
}

export interface RecipesPage {
  items: RecipeListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export const RECIPE_SORTS = ['rating', 'recentlyEaten', 'notEatenLately', 'prepTime', 'name'] as const;
export type RecipeSort = (typeof RECIPE_SORTS)[number];

/** What the library asks `GET /api/recipes` for; every `tags` entry must be on a recipe. */
export interface RecipeLibraryFilter {
  search?: string;
  tags?: readonly string[];
  withoutNever?: boolean;
  archived?: boolean;
  sort?: RecipeSort;
}

export const RECIPES_MAX_PAGE_SIZE = 200;

export function libraryParams(filter: RecipeLibraryFilter, page: number, pageSize: number): Record<string, string | string[]> {
  const params: Record<string, string | string[]> = { page: String(page), pageSize: String(pageSize) };
  if (filter.search) {
    params['search'] = filter.search;
  }
  if (filter.tags?.length) {
    params['tag'] = [...filter.tags];
  }
  if (filter.withoutNever) {
    params['withoutNever'] = 'true';
  }
  if (filter.archived) {
    params['archived'] = 'true';
  }
  if (filter.sort) {
    params['sort'] = filter.sort;
  }
  return params;
}

/** A tag for autocomplete and how many recipes that are not archived use it. */
export interface TagUsage {
  tag: string;
  recipes: number;
}

/** Writes of the Recipes module and the paged library. Other reads use `httpResource`. */
@Injectable({ providedIn: 'root' })
export class RecipesApi {
  private readonly http = inject(HttpClient);

  libraryPage(filter: RecipeLibraryFilter, page: number, pageSize: number): Promise<RecipesPage> {
    return firstValueFrom(this.http.get<RecipesPage>(RECIPES_URL, { params: libraryParams(filter, page, pageSize) }));
  }

  createRecipe(change: RecipeChange): Promise<Recipe> {
    return firstValueFrom(this.http.post<Recipe>(RECIPES_URL, change));
  }

  updateRecipe(id: string, change: RecipeChange): Promise<Recipe> {
    return firstValueFrom(this.http.put<Recipe>(recipeUrl(id), change));
  }
}
