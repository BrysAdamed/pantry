import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { EmptyState, EmptyStateIllustration } from '../shared/empty-state/empty-state';
import { Destination } from './destinations';

export type PlaceholderDestination = Exclude<Destination, 'household' | 'ingredients' | 'recipes'>;

const PLACEHOLDERS: Record<PlaceholderDestination, { illustration: EmptyStateIllustration; agentHint: boolean }> = {
  planner: { illustration: 'planner', agentHint: true },
  shopping: { illustration: 'lists', agentHint: false },
};

/** Stands in for a destination until its epic lands. Set the destination as the `placeholder` route data. */
@Component({
  selector: 'app-feature-placeholder',
  imports: [TranslocoDirective, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-empty-state
      *transloco="let t; prefix: 'emptyState.' + placeholder()"
      class="mx-auto mt-6 md:mt-16"
      [illustration]="config().illustration"
      [heading]="t('title')"
      [description]="t('description')"
      [agentHint]="config().agentHint ? t('agentHint') : undefined"
    />
  `,
})
export class FeaturePlaceholder {
  readonly placeholder = input.required<PlaceholderDestination>();

  protected readonly config = computed(() => PLACEHOLDERS[this.placeholder()]);
}
