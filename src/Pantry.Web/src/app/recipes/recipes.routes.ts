import { Routes } from '@angular/router';
import { FeaturePlaceholder } from '../shell/feature-placeholder';

export const recipesRoutes: Routes = [{ path: '', component: FeaturePlaceholder, data: { placeholder: 'recipes' } }];
