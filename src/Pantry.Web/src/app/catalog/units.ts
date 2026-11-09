export const UNITS = [
  'g',
  'kg',
  'ml',
  'l',
  'piece',
  'clove',
  'tbsp',
  'tsp',
  'pinch',
  'can',
  'jar',
  'pack',
  'slice',
  'bunch',
  'handful',
  'glass',
] as const;

export type Unit = (typeof UNITS)[number];

/** The units an ingredient can be summed in, in the order the UI offers them. */
export const BASE_UNITS = ['g', 'ml', 'piece'] as const satisfies readonly Unit[];

export type BaseUnit = (typeof BASE_UNITS)[number];

export interface GenericConversion {
  readonly unit: Unit;
  readonly baseUnit: BaseUnit;
  readonly factor: number;
}

/** Conversions that hold for every ingredient, the same as the API's `Units` (and `GET /api/catalog/vocabulary`). */
export const GENERIC_CONVERSIONS: readonly GenericConversion[] = [
  { unit: 'kg', baseUnit: 'g', factor: 1000 },
  { unit: 'l', baseUnit: 'ml', factor: 1000 },
  { unit: 'tbsp', baseUnit: 'ml', factor: 15 },
  { unit: 'tsp', baseUnit: 'ml', factor: 5 },
];

/** True when `unit` needs no ingredient conversion into `baseUnit`: it is the base unit or converts into it on its own. */
export function isRedundantConversion(unit: Unit, baseUnit: BaseUnit): boolean {
  return unit === baseUnit || GENERIC_CONVERSIONS.some((c) => c.unit === unit && c.baseUnit === baseUnit);
}

export function isBaseUnit(unit: Unit): unit is BaseUnit {
  return (BASE_UNITS as readonly Unit[]).includes(unit);
}
