import { CanDeactivateFn, Routes } from '@angular/router';
import { IngredientDetail } from './ingredient-detail/ingredient-detail';
import { IngredientsPage } from './ingredients-page';
import { NEW_INGREDIENT_PATH } from './ingredients-paths';
import { NoIngredientOpen } from './no-ingredient-open';

/** Leaving an ingredient with unsaved changes (back, another ingredient, another destination) asks first. */
export const confirmDiscardChanges: CanDeactivateFn<IngredientDetail> = (detail) => detail.canLeave();

export const ingredientsRoutes: Routes = [
  {
    path: '',
    component: IngredientsPage,
    children: [
      { path: '', component: NoIngredientOpen },
      { path: NEW_INGREDIENT_PATH, component: IngredientDetail, canDeactivate: [confirmDiscardChanges] },
      { path: ':id', component: IngredientDetail, canDeactivate: [confirmDiscardChanges] },
    ],
  },
];
