import { CanDeactivateFn, Routes } from '@angular/router';
import { FeaturePlaceholder } from '../shell/feature-placeholder';
import { RecipeEditor } from './editor/recipe-editor';
import { EDIT_RECIPE_PATH, NEW_RECIPE_PATH } from './recipes-paths';

/** Leaving the editor with unsaved changes asks first. */
export const confirmDiscardRecipe: CanDeactivateFn<RecipeEditor> = (editor) => editor.canLeave();

export const recipesRoutes: Routes = [
  { path: '', component: FeaturePlaceholder, data: { placeholder: 'recipes' } },
  { path: NEW_RECIPE_PATH, component: RecipeEditor, canDeactivate: [confirmDiscardRecipe] },
  {
    path: `:id/${EDIT_RECIPE_PATH}`,
    component: RecipeEditor,
    canDeactivate: [confirmDiscardRecipe],
  },
];
