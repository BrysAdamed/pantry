export const INGREDIENTS_PATH = '/ingredients';
export const NEW_INGREDIENT_PATH = 'new';
export const POSSIBLE_DUPLICATES_PATH = 'duplicates';

export function ingredientPath(id: string): string[] {
  return [INGREDIENTS_PATH, id];
}
