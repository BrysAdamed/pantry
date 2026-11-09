import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { PossibleDuplicate } from '../catalog/catalog-api';
import { Icon } from '../shared/icon/icon';
import { IngredientTexts } from './ingredient-texts';
import { IngredientsScreen } from './ingredients-screen';
import { IngredientMerges } from './merge/ingredient-merges';

/** Possible duplicates (`/ingredients/duplicates`): pairs with similar names, each merged or marked as not duplicates. */
@Component({
  selector: 'app-possible-duplicates',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  template: `
    <ng-container *transloco="let t">
      <a
        routerLink="/ingredients"
        class="inline-flex min-h-tap items-center gap-1.5 justify-self-start rounded-control pr-3 font-bold text-muted-foreground hover:text-foreground md:hidden"
        data-testid="ingredient-back"
      >
        <app-icon name="chevronLeft" class="size-5" />
        {{ t('ingredients.back') }}
      </a>

      <header class="grid gap-2">
        <h2 class="text-[26px] leading-tight md:text-[34px]">
          {{ t('ingredients.duplicates.title') }}
        </h2>
        <p class="text-sm text-muted-foreground">{{ t('ingredients.duplicates.text') }}</p>
      </header>

      @if (screen.duplicatesFailed()) {
        <div
          class="flex flex-wrap items-center gap-3 rounded-control border-2 border-danger bg-danger-surface px-3.5 py-3 text-sm"
          role="alert"
        >
          <span class="flex-1">{{ t('ingredients.duplicates.loadFailed') }}</span>
          <button hlmBtn variant="outline" size="sm" type="button" (click)="screen.reloadDuplicates()">
            {{ t('ingredients.retry') }}
          </button>
        </div>
      } @else if (screen.duplicatesLoaded()) {
        <ul class="grid gap-3">
          @for (pair of screen.duplicatePairs(); track pair.first.id + pair.second.id) {
            <li class="grid gap-3 rounded-card border-2 border-outline bg-card px-4 py-3.5 shadow-card" data-testid="duplicate-pair">
              <div class="grid grid-cols-[minmax(0,1fr)_28px_minmax(0,1fr)] items-center gap-2">
                @for (side of [pair.first, pair.second]; track side.id; let second = $last) {
                  @if (second) {
                    <span class="grid place-items-center text-ingredient-duplicate" aria-hidden="true"
                      ><app-icon name="twins" class="size-5"
                    /></span>
                  }
                  <a
                    class="grid min-w-0 gap-0.5 rounded-control bg-secondary px-2.5 py-2 hover:underline"
                    data-testid="duplicate-side"
                    [routerLink]="['/ingredients', side.id]"
                  >
                    <b class="[overflow-wrap:anywhere]">{{ side.name }}</b>
                    <span class="text-xs text-muted-foreground"
                      >{{ t('catalog.category.' + side.category) }} · {{ texts.unit(side.baseUnit) }}</span
                    >
                    @if (side.needsReview) {
                      <span>
                        <span
                          class="inline-flex items-center rounded-full border-[1.5px] border-current bg-ingredient-review-surface px-1.75 py-0.5 text-[11px] font-extrabold text-ingredient-review"
                          >{{ t('ingredients.badge.needsReview') }}</span
                        >
                      </span>
                    }
                  </a>
                }
              </div>
              <div class="flex flex-wrap gap-2">
                <button hlmBtn size="sm" type="button" [disabled]="busy()" (click)="merge(pair)">
                  <app-icon name="merge" class="size-4" />
                  {{ t('ingredients.merge.pair') }}
                </button>
                <button hlmBtn variant="ghost" size="sm" type="button" [disabled]="busy()" (click)="notDuplicates(pair)">
                  {{ t('ingredients.duplicates.notDuplicates') }}
                </button>
              </div>
            </li>
          } @empty {
            <li class="grid justify-items-center gap-3 px-3 py-7 text-center text-sm text-muted-foreground" data-testid="no-duplicates">
              <app-icon name="check" class="size-6 text-success" />
              <h3 class="text-[22px] text-foreground">
                {{ t('ingredients.duplicates.emptyTitle') }}
              </h3>
              <p>{{ t('ingredients.duplicates.emptyText') }}</p>
            </li>
          }
        </ul>
      }
    </ng-container>
  `,
})
export class PossibleDuplicates {
  private readonly merges = inject(IngredientMerges);
  protected readonly screen = inject(IngredientsScreen);
  protected readonly texts = inject(IngredientTexts);
  protected readonly busy = signal(false);

  protected async merge(pair: PossibleDuplicate): Promise<void> {
    await this.run(() => this.merges.merge(pair.first, pair.second));
  }

  protected async notDuplicates(pair: PossibleDuplicate): Promise<void> {
    await this.run(() => this.merges.markNotDuplicates(pair.first, pair.second));
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } finally {
      this.busy.set(false);
    }
  }
}
