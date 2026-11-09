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
