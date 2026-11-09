import { Unit } from '../../catalog/units';
import { RECIPE_LIMITS } from '../recipes-api';

export const SERVINGS_STEP = 0.5;
export const MIN_SERVINGS = SERVINGS_STEP;

const METRIC_IN_SMALLER: Partial<Record<Unit, number>> = { g: 1, ml: 1, kg: 1000, l: 1000 };
const ROUND_TO_FIVE_FROM = 20;
const FIVE = 5;
const TENTH = 0.1;

/**
 * The servings the detail starts with: the household serving, or the recipe servings when the household has nobody
 * eating yet.
 */
export function startingServings(householdServing: number, recipeServings: number): number {
  return householdServing > 0 ? householdServing : recipeServings;
}

/** One stepper step to the next half serving up or down (2.3 goes to 2.5 or 2), within the recipe limits. */
export function stepServings(servings: number, direction: 1 | -1): number {
  const halves = servings / SERVINGS_STEP;
  const next = direction === 1 ? Math.floor(halves + 1e-9) + 1 : Math.ceil(halves - 1e-9) - 1;
  return Math.min(RECIPE_LIMITS.maxServings, Math.max(MIN_SERVINGS, next * SERVINGS_STEP));
}

/** A per-serving quantity for `servings`, rounded so it can be measured (see `roundForMeasuring`). */
export function scaledAmount(quantityPerServing: number, unit: Unit, servings: number): number {
  return roundForMeasuring(quantityPerServing * servings, unit);
}

/**
 * Rounds an amount so a cook can measure it: grams and millilitres to 5 from 20 up and to whole ones below (tiny
 * amounts to a tenth, never to zero), kilograms and litres the same way counted in grams and millilitres, and
 * everything counted in pieces, cloves, spoons or other portions to halves, at least a half.
 */
export function roundForMeasuring(amount: number, unit: Unit): number {
  if (amount <= 0) {
    return 0;
  }
  const smaller = METRIC_IN_SMALLER[unit];
  if (smaller) {
    return roundMetric(amount * smaller) / smaller;
  }
  return Math.max(SERVINGS_STEP, Math.round(amount / SERVINGS_STEP) * SERVINGS_STEP);
}

function roundMetric(amount: number): number {
  if (amount >= ROUND_TO_FIVE_FROM) {
    return Math.round(amount / FIVE) * FIVE;
  }
  if (amount >= 1) {
    return Math.round(amount);
  }
  return Math.max(TENTH, Math.round(amount / TENTH) * TENTH);
}
