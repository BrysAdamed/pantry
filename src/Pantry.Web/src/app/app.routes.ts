import { Routes } from '@angular/router';
import { FIRST_RUN_PATH, requireFirstRunNeeded, requireSetupDone } from './identity/setup-status';
import { requireSignedIn, requireSignedOut, SIGN_IN_PATH } from './identity/session';
import { REDEEM_PATH } from './identity/user-admin-api';
import { Shell } from './shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    canActivate: [requireSetupDone, requireSignedIn],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'planner' },
      { path: 'planner', loadChildren: () => import('./planner/planner.routes').then((m) => m.plannerRoutes) },
      { path: 'recipes', loadChildren: () => import('./recipes/recipes.routes').then((m) => m.recipesRoutes) },
      { path: 'shopping', loadChildren: () => import('./shopping/shopping.routes').then((m) => m.shoppingRoutes) },
      { path: 'household', loadChildren: () => import('./household/household.routes').then((m) => m.householdRoutes) },
    ],
  },
  {
    path: FIRST_RUN_PATH,
    canActivate: [requireFirstRunNeeded],
    loadComponent: () => import('./identity/first-run/first-run').then((m) => m.FirstRun),
  },
  {
    path: SIGN_IN_PATH,
    canActivate: [requireSetupDone, requireSignedOut],
    loadComponent: () => import('./identity/sign-in/sign-in').then((m) => m.SignIn),
  },
  {
    path: `${REDEEM_PATH}/:token`,
    loadComponent: () => import('./identity/redeem-link/redeem-link').then((m) => m.RedeemLink),
  },
  {
    path: 'design-system',
    loadComponent: () => import('./design-system/token-showcase').then((m) => m.TokenShowcase),
  },
  { path: '**', redirectTo: '' },
];
