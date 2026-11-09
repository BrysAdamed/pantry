import {
  CONVERSION_AMOUNT_DECIMALS,
  CONVERSION_AMOUNT_MAX,
  DEFAULT_INGREDIENT_CATEGORY,
  Ingredient,
  INGREDIENT_NAME_MAX_LENGTH,
  IngredientCategory,
  IngredientChange,
  UnitConversion,
} from '../../catalog/catalog-api';
import { cleanIngredientName, ingredientNameKey } from '../../catalog/ingredient-name';
import { BaseUnit, isBaseUnit, isRedundantConversion, Unit, UNITS } from '../../catalog/units';

export const MAX_ALIASES = 20;
export const DEFAULT_BASE_UNIT: BaseUnit = 'g';

/** The ingredient form as a person edits it; the API gets it as an `IngredientChange`. */
export interface IngredientDraft {
  readonly name: string;
  readonly aliases: readonly string[];
  readonly category: IngredientCategory;
  readonly baseUnit: BaseUnit;
  readonly isStaple: boolean;
  readonly conversions: readonly UnitConversion[];
}

export type AliasProblem = 'empty' | 'repeated' | 'tooMany';

export type AmountProblem = 'notPositive' | 'invalid';

export function draftOf(ingredient: Ingredient): IngredientDraft {
  return {
    name: ingredient.name,
    aliases: ingredient.aliases,
    category: ingredient.category,
    baseUnit: isBaseUnit(ingredient.baseUnit) ? ingredient.baseUnit : DEFAULT_BASE_UNIT,
    isStaple: ingredient.isStaple,
    conversions: ingredient.conversions,
  };
}

export function newDraft(name = ''): IngredientDraft {
  return {
    name: cleanIngredientName(name).slice(0, INGREDIENT_NAME_MAX_LENGTH),
    aliases: [],
    category: DEFAULT_INGREDIENT_CATEGORY,
    baseUnit: DEFAULT_BASE_UNIT,
    isStaple: false,
    conversions: [],
  };
}

export function sameDraft(a: IngredientDraft, b: IngredientDraft): boolean {
  return (
    a.name === b.name &&
    a.category === b.category &&
    a.baseUnit === b.baseUnit &&
    a.isStaple === b.isStaple &&
    a.aliases.length === b.aliases.length &&
    a.aliases.every((alias, index) => alias === b.aliases[index]) &&
    a.conversions.length === b.conversions.length &&
    a.conversions.every((c, index) => c.unit === b.conversions[index].unit && c.amount === b.conversions[index].amount)
  );
}

export function changeOf(draft: IngredientDraft): IngredientChange {
  return {
    name: cleanIngredientName(draft.name),
    aliases: draft.aliases.map(cleanIngredientName),
    category: draft.category,
    baseUnit: draft.baseUnit,
    isStaple: draft.isStaple,
    conversions: [...draft.conversions],
  };
}

export function hasName(draft: IngredientDraft): boolean {
  return ingredientNameKey(draft.name).length > 0;
}

/** Why `alias` cannot be added to the draft, or null when it can. Names compare ignoring case and diacritics. */
export function aliasProblem(draft: IngredientDraft, alias: string): AliasProblem | null {
  const key = ingredientNameKey(alias);
  if (!key) {
    return 'empty';
  }
  if ([draft.name, ...draft.aliases].some((name) => ingredientNameKey(name) === key)) {
    return 'repeated';
  }
  return draft.aliases.length >= MAX_ALIASES ? 'tooMany' : null;
}

/** Units a conversion can still be added for: not the base unit's own family, not converted already. */
export function conversionUnits(draft: IngredientDraft): Unit[] {
  return UNITS.filter((unit) => !isRedundantConversion(unit, draft.baseUnit) && !draft.conversions.some((c) => c.unit === unit));
}

/** Reads an amount typed with a decimal point or comma. */
export function parseAmount(text: string): number | AmountProblem {
  const normalized = text.trim().replace(/\s/g, '').replace(',', '.');
  if (!/^\d*\.?\d+$|^\d+\.$/.test(normalized)) {
    return normalized === '' || normalized.startsWith('-') ? 'notPositive' : 'invalid';
  }
  const amount = Number(normalized);
  if (!(amount > 0)) {
    return 'notPositive';
  }
  const decimals = normalized.includes('.') ? normalized.split('.')[1].length : 0;
  return amount > CONVERSION_AMOUNT_MAX || decimals > CONVERSION_AMOUNT_DECIMALS ? 'invalid' : amount;
}
