export const INGREDIENTS_PATH = '/ingredients';
export const NEW_INGREDIENT_PATH = 'new';

export function ingredientPath(id: string): string[] {
  return [INGREDIENTS_PATH, id];
}
