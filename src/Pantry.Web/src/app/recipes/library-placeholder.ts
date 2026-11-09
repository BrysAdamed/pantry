import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeaturePlaceholder } from '../shell/feature-placeholder';
import { ToRateCards } from './rating/to-rate-cards';

// TODO: PAN-47 replaces this with the recipe library, which shows <app-to-rate-cards /> at the top of its list.
@Component({
  selector: 'app-library-placeholder',
  imports: [FeaturePlaceholder, ToRateCards],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  template: `
    <app-to-rate-cards class="mx-auto w-full max-w-xl" />
    <app-feature-placeholder placeholder="recipes" />
  `,
})
export class LibraryPlaceholder {}
