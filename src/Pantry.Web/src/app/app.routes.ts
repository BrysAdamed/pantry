import { Routes } from '@angular/router';
import { Home } from './home/home';
import { FIRST_RUN_PATH, requireFirstRunNeeded, requireSetupDone } from './identity/setup-status';

export const routes: Routes = [
  { path: '', component: Home, canActivate: [requireSetupDone] },
  {
    path: FIRST_RUN_PATH,
    canActivate: [requireFirstRunNeeded],
    loadComponent: () => import('./identity/first-run/first-run').then((m) => m.FirstRun),
  },
  {
    path: 'design-system',
    loadComponent: () => import('./design-system/token-showcase').then((m) => m.TokenShowcase),
  },
];
