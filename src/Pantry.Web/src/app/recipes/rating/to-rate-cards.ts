import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { MEAL_SLOTS_URL, MealSlot } from '../../household/household-api';
import { nearDayOf, RECIPE_DATE_FORMAT } from '../local-date';
import { TO_RATE_URL, ToRateItem } from '../recipes-api';
import { RatingPrompt } from './rating-prompt';
import { RatingOutcome } from './rating-sheet';

/**
 * The *to rate* cards at the top of the library (*Do oceny*): eaten dishes of the last days nobody rated yet, each
 * with *Rate*, which opens the rating sheet. Shows nothing when there is nothing to rate.
 */
@Component({
  selector: 'app-to-rate-cards',
  imports: [TranslocoDirective, HlmButton, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid gap-2.5 empty:hidden' },
  template: `
    <ng-container *transloco="let t; prefix: 'recipes.rating'">
      @for (item of items(); track item.id) {
        <section
          class="grid gap-1.5 rounded-card border-2 border-outline bg-accent px-3.5 py-3 text-accent-foreground shadow-card"
          [attr.aria-label]="t('toRateLabel', { name: item.recipeName })"
          data-testid="to-rate-card"
        >
          <span class="text-xs font-bold tracking-[0.08em] uppercase">{{ t('toRate') }}</span>
          <div class="flex items-center justify-between gap-3">
            <div class="grid min-w-0">
              <b class="[overflow-wrap:anywhere]">{{ item.recipeName }}</b>
              <span class="text-sm">
                {{ dayOf(item) ? t('day.' + dayOf(item)) : (item.date | date: dateFormat : undefined : language()) }}
                @if (slotName(item); as slot) {
                  · {{ slot }}
                }
              </span>
            </div>
            <button hlmBtn variant="outline" size="sm" type="button" data-testid="to-rate-rate" (click)="rate(item)">
              {{ t('rate') }}
            </button>
          </div>
        </section>
      }
    </ng-container>
  `,
})
export class ToRateCards {
  private readonly prompt = inject(RatingPrompt);
  protected readonly language = inject(UiLanguageService).current;
  protected readonly dateFormat = RECIPE_DATE_FORMAT;

  /** After a rating was saved, so the screen around can refresh what it shows. */
  readonly rated = output<RatingOutcome>();

  private readonly toRate = httpResource<ToRateItem[]>(() => TO_RATE_URL);
  private readonly mealSlots = httpResource<MealSlot[]>(() => MEAL_SLOTS_URL);
  protected readonly items = computed(() => (this.toRate.hasValue() ? this.toRate.value() : []));
  private readonly slotNames = computed(
    () => new Map((this.mealSlots.value() ?? []).map((slot) => [slot.id, slot.name])),
  );

  protected dayOf(item: ToRateItem): 'today' | 'yesterday' | null {
    return nearDayOf(item.date);
  }

  protected slotName(item: ToRateItem): string | null {
    return this.slotNames().get(item.mealSlotId) ?? null;
  }

  protected async rate(item: ToRateItem): Promise<void> {
    const outcome = await this.prompt.open({
      entryId: item.id,
      recipeName: item.recipeName,
      date: item.date,
      mealSlotName: this.slotName(item),
      rating: null,
      makeAgain: null,
    });
    if (outcome) {
      this.toRate.reload();
      this.rated.emit(outcome);
    }
  }
}
