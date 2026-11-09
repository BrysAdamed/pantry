import { Routes } from '@angular/router';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', component: Home },
  {
    path: 'design-system',
    loadComponent: () => import('./design-system/token-showcase').then((m) => m.TokenShowcase),
  },
];
