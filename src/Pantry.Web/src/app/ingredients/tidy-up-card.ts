import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { Icon } from '../shared/icon/icon';
import { IngredientsScreen } from './ingredients-screen';
import { INGREDIENTS_PATH, POSSIBLE_DUPLICATES_PATH } from './ingredients-paths';

/**
 * "Tidy up" at the top of the list: the work that waits for a person, one line per kind (ingredients to review,
 * possible duplicates), hidden when there is nothing to do.
 */
@Component({
  selector: 'app-tidy-up-card',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
  template: `
    @if (hasWork()) {
      <section
        *transloco="let t"
        class="grid gap-0.5 rounded-card border-2 border-dashed border-outline bg-card px-3 py-2.5"
        aria-labelledby="tidy-up-title"
        data-testid="tidy-up"
      >
        <h3 id="tidy-up-title" class="font-sans text-xs font-bold tracking-[0.08em] text-muted-foreground uppercase">
          {{ t('ingredients.tidy.title') }}
        </h3>
        @if (needsReview() > 0) {
          <div class="flex min-h-10 items-center gap-2.5 text-sm">
            <span class="grid size-6.5 shrink-0 place-items-center rounded-full bg-ingredient-review-surface text-ingredient-review">
              <app-icon name="sparkle" class="size-3.5" />
            </span>
            <span class="flex-1">{{ t('ingredients.tidy.review', { count: needsReview() }) }}</span>
            <button hlmBtn variant="outline" size="sm" type="button" (click)="toggleReview()">
              {{ screen.needsReviewOnly() ? t('ingredients.tidy.showAll') : t('ingredients.tidy.startReview') }}
            </button>
          </div>
        }
        @if (duplicates() > 0) {
          <div class="flex min-h-10 items-center gap-2.5 text-sm" data-testid="tidy-up-duplicates">
            <span class="grid size-6.5 shrink-0 place-items-center rounded-full bg-ingredient-duplicate-surface text-ingredient-duplicate">
              <app-icon name="twins" class="size-3.5" />
            </span>
            <span class="flex-1">{{ t('ingredients.tidy.duplicates', { count: duplicates() }) }}</span>
            <a hlmBtn variant="outline" size="sm" [routerLink]="duplicatesPath">{{ t('ingredients.tidy.startReview') }}</a>
          </div>
        }
      </section>
    }
  `,
})
export class TidyUpCard {
  protected readonly screen = inject(IngredientsScreen);
  protected readonly needsReview = this.screen.needsReviewCount;
  protected readonly duplicates = computed(() => this.screen.duplicatePairs().length);
  protected readonly hasWork = computed(() => this.needsReview() > 0 || this.duplicates() > 0);
  protected readonly duplicatesPath = [INGREDIENTS_PATH, POSSIBLE_DUPLICATES_PATH];

  protected toggleReview(): void {
    this.screen.needsReviewOnly.set(!this.screen.needsReviewOnly());
  }
}
