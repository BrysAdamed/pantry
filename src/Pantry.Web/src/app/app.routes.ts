import { Routes } from '@angular/router';
import { Shell } from './shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'planner' },
      { path: 'planner', loadChildren: () => import('./planner/planner.routes').then((m) => m.plannerRoutes) },
      { path: 'recipes', loadChildren: () => import('./recipes/recipes.routes').then((m) => m.recipesRoutes) },
      { path: 'shopping', loadChildren: () => import('./shopping/shopping.routes').then((m) => m.shoppingRoutes) },
      { path: 'household', loadChildren: () => import('./household/household.routes').then((m) => m.householdRoutes) },
    ],
  },
  {
    path: 'design-system',
    loadComponent: () => import('./design-system/token-showcase').then((m) => m.TokenShowcase),
  },
  { path: '**', redirectTo: '' },
];
