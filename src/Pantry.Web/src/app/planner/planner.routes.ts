import { Routes } from '@angular/router';
import { FeaturePlaceholder } from '../shell/feature-placeholder';

export const plannerRoutes: Routes = [{ path: '', component: FeaturePlaceholder, data: { placeholder: 'planner' } }];
