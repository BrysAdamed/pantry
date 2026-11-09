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

/** What the API accepts as a recipe photo (ADR 0025); it recognises the picture by its content. */
export const RECIPE_PHOTO_LIMITS = {
  maxBytes: 10 * 1024 * 1024,
  types: ['image/jpeg', 'image/png', 'image/webp'],
} as const;

export function recipeUrl(id: string): string {
  return `${RECIPES_URL}/${id}`;
}

export function recipePhotoUrl(id: string): string {
  return `${recipeUrl(id)}/photo`;
}

/** Where the sizes of a recipe's photo are served: a square `card` and the whole picture for the `detail`. */
export interface RecipePhoto {
  card: string;
  detail: string;
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

/**
 * When a recipe should come back: the cooldown in effect (0 for none) and the first day that is not *too soon*
 * (`availableFrom`, last eaten + cooldown). Dates are `yyyy-MM-dd`; null when never eaten, which means available now.
 */
export interface RecipeCooldown {
  days: number;
  isHouseholdDefault: boolean;
  lastEaten: string | null;
  availableFrom: string | null;
}

/** One time the household ate a recipe; `rating` is 1 to 5 stars, null while not rated. */
export interface HistoryEntry {
  id: string;
  recipeId: string;
  date: string;
  mealSlotId: string;
  planEntryId: string;
  rating: number | null;
}

/** What the household now thinks of a recipe, returned by rating writes. */
export interface RatingSummary {
  recipeId: string;
  makeAgain: boolean;
  averageRating: number | null;
  ratingCount: number;
  isFavourite: boolean;
}

export interface RatingResult {
  entry: HistoryEntry;
  recipe: RatingSummary;
}

export const TO_RATE_URL = `${RECIPES_URL}/history/to-rate`;

/** A history entry of the last days still waiting for a rating, newest first. */
export interface ToRateItem {
  id: string;
  recipeId: string;
  recipeName: string;
  recipeIsArchived: boolean;
  date: string;
  mealSlotId: string;
  planEntryId: string;
}

export function historyRatingUrl(entryId: string): string {
  return `${RECIPES_URL}/history/${entryId}/rating`;
}

export function recipeHistoryUrl(id: string): string {
  return `${recipeUrl(id)}/history`;
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
  /** Over the rated history entries, rounded to one decimal; null when none is rated. */
  averageRating: number | null;
  ratingCount: number;
  /** Make again and an average rating of at least 4. */
  isFavourite: boolean;
  /** The recipe's own cooldown; null uses the household default. */
  cooldownDays: number | null;
  cooldown: RecipeCooldown;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  photo: RecipePhoto | null;
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
  photo: RecipePhoto | null;
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

  archiveRecipe(id: string): Promise<Recipe> {
    return firstValueFrom(this.http.post<Recipe>(`${recipeUrl(id)}/archive`, null));
  }

  restoreRecipe(id: string): Promise<Recipe> {
    return firstValueFrom(this.http.post<Recipe>(`${recipeUrl(id)}/restore`, null));
  }

  /** Sends the picture itself as the body; 413 when it is too large, 415 when it is not a JPEG, PNG or WebP picture. */
  setPhoto(id: string, photo: Blob): Promise<Recipe> {
    return firstValueFrom(
      this.http.put<Recipe>(recipePhotoUrl(id), photo, {
        headers: { 'Content-Type': photo.type || 'application/octet-stream' },
      }),
    );
  }

  removePhoto(id: string): Promise<Recipe> {
    return firstValueFrom(this.http.delete<Recipe>(recipePhotoUrl(id)));
  }

  /** Adds or changes the stars of a history entry; a `makeAgain` answer also sets the recipe's flag. */
  rate(entryId: string, stars: number, makeAgain: boolean | null): Promise<RatingResult> {
    const body = makeAgain === null ? { stars } : { stars, makeAgain };
    return firstValueFrom(this.http.put<RatingResult>(historyRatingUrl(entryId), body));
  }

  removeRating(entryId: string): Promise<RatingResult> {
    return firstValueFrom(this.http.delete<RatingResult>(historyRatingUrl(entryId)));
  }

  setMakeAgain(id: string, makeAgain: boolean): Promise<RatingSummary> {
    return firstValueFrom(this.http.put<RatingSummary>(`${recipeUrl(id)}/make-again`, { makeAgain }));
  }
}
