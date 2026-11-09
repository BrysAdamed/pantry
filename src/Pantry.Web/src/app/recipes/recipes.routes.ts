import { CanDeactivateFn, Routes } from '@angular/router';
import { RecipeDetail } from './detail/recipe-detail';
import { RecipeEditor } from './editor/recipe-editor';
import { NoRecipeOpen } from './library/no-recipe-open';
import { RecipeLibrary } from './library/recipe-library';
import { EDIT_RECIPE_PATH, NEW_RECIPE_PATH } from './recipes-paths';

/** Leaving the editor with unsaved changes asks first. */
export const confirmDiscardRecipe: CanDeactivateFn<RecipeEditor> = (editor) => editor.canLeave();

export const recipesRoutes: Routes = [
  { path: NEW_RECIPE_PATH, component: RecipeEditor, canDeactivate: [confirmDiscardRecipe] },
  {
    path: `:id/${EDIT_RECIPE_PATH}`,
    component: RecipeEditor,
    canDeactivate: [confirmDiscardRecipe],
  },
  {
    path: '',
    component: RecipeLibrary,
    children: [
      { path: '', component: NoRecipeOpen },
      { path: ':id', component: RecipeDetail },
    ],
  },
];
