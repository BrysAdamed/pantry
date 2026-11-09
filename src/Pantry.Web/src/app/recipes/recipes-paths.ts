export const RECIPES_PATH = '/recipes';
export const NEW_RECIPE_PATH = 'new';
export const EDIT_RECIPE_PATH = 'edit';

export function newRecipePath(): string[] {
  return [RECIPES_PATH, NEW_RECIPE_PATH];
}

export function editRecipePath(id: string): string[] {
  return [RECIPES_PATH, id, EDIT_RECIPE_PATH];
}
