import { RecipeListItem } from '../recipes-api';

/** Tile colours for a recipe without a photo, picked by the recipe id so a card keeps its colour. */
export const RECIPE_TILE_COLOURS = [
  'var(--accent)',
  'var(--agent-surface)',
  'var(--secondary)',
  'var(--success-surface)',
  'var(--warning-surface)',
  'var(--danger-surface)',
] as const;

export function recipeTileColour(id: string): string {
  let hash = 0;
  for (const character of id) {
    hash = (hash * 31 + character.charCodeAt(0)) >>> 0;
  }
  return RECIPE_TILE_COLOURS[hash % RECIPE_TILE_COLOURS.length];
}

export function recipeInitial(name: string): string {
  return [...name.trim()][0]?.toLocaleUpperCase() ?? '';
}

/** Planning the recipe on `today` is *too soon*: its cooldown ends later. */
export function isTooSoon(recipe: RecipeListItem, today: string): boolean {
  const from = recipe.cooldown.availableFrom;
  return !!from && today < from;
}

/** Whole days from `date` to `today` (both `yyyy-mm-dd`). */
export function daysBetween(date: string, today: string): number {
  return Math.round((Date.parse(today) - Date.parse(date)) / 86_400_000);
}

/** When the recipe was last eaten, in the words of `language` (*wczoraj*, *2 tyg. temu*); null when never. */
export function lastEatenText(lastEaten: string | null, today: string, language: string): string | null {
  if (!lastEaten) {
    return null;
  }
  const days = Math.max(0, daysBetween(lastEaten, today));
  const format = new Intl.RelativeTimeFormat(language, { numeric: 'auto', style: 'short' });
  if (days < 7) {
    return format.format(-days, 'day');
  }
  if (days < 60) {
    return format.format(-Math.floor(days / 7), 'week');
  }
  if (days < 730) {
    return format.format(-Math.floor(days / 30), 'month');
  }
  return format.format(-Math.floor(days / 365), 'year');
}

export function hasDislike(recipe: RecipeListItem, kind: 'never' | 'avoid'): boolean {
  return recipe.dislikes.some((dislike) => dislike.kind === kind);
}
