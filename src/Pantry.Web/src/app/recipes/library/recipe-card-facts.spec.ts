import { RecipeListItem } from '../recipes-api';
import { daysBetween, filledStars, isTooSoon, lastEatenText, localToday, recipeInitial, RECIPE_TILE_COLOURS, recipeTileColour } from './recipe-card-facts';

const withCooldown = (availableFrom: string | null) =>
  ({ cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom } }) as RecipeListItem;

describe('recipe card facts', () => {
  it('writes the local date as the API does', () => {
    expect(localToday(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05');
  });

  it('is too soon only before the available from date', () => {
    expect(isTooSoon(withCooldown('2026-11-12'), '2026-11-11')).toBe(true);
    expect(isTooSoon(withCooldown('2026-11-12'), '2026-11-12')).toBe(false);
    expect(isTooSoon(withCooldown(null), '2026-11-12')).toBe(false);
  });

  it('says when a recipe was last eaten in days, weeks, months and years', () => {
    expect(daysBetween('2026-10-31', '2026-11-10')).toBe(10);
    expect(lastEatenText(null, '2026-11-10', 'en')).toBeNull();
    expect(lastEatenText('2026-11-10', '2026-11-10', 'en')).toBe('today');
    expect(lastEatenText('2026-11-09', '2026-11-10', 'pl')).toBe('wczoraj');
    expect(lastEatenText('2026-11-05', '2026-11-10', 'en')).toBe('5 days ago');
    expect(lastEatenText('2026-10-27', '2026-11-10', 'en')).toBe('2 wk. ago');
    expect(lastEatenText('2026-08-10', '2026-11-10', 'en')).toBe('3 mo. ago');
    expect(lastEatenText('2023-11-10', '2026-11-10', 'en')).toBe('3 yr. ago');
  });

  it('fills stars to the nearest whole star', () => {
    expect(filledStars(null)).toBe(0);
    expect(filledStars(4.5)).toBe(5);
    expect(filledStars(3.4)).toBe(3);
  });

  it('keeps the tile colour of a recipe and uses its initial', () => {
    expect(recipeTileColour('curry')).toBe(recipeTileColour('curry'));
    expect(RECIPE_TILE_COLOURS).toContain(recipeTileColour('salmon'));
    expect(recipeInitial('  łosoś')).toBe('Ł');
    expect(recipeInitial('')).toBe('');
  });
});
