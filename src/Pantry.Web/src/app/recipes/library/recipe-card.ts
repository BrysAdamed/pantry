import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { Icon } from '../../shared/icon/icon';
import { RecipeListItem } from '../recipes-api';
import { recipePath } from '../recipes-paths';
import { Stars } from '../stars';
import { hasDislike, isTooSoon, lastEatenText, recipeInitial, recipeTileColour } from './recipe-card-facts';

/**
 * One recipe in the library: a coloured tile with the initial, the name, rating, prep time, when it was last eaten and
 * kcal, and the *never*, *avoid*, *not cooking again* and *too soon* badges.
 */
@Component({
  selector: 'app-recipe-card',
  imports: [RouterLink, TranslocoDirective, Icon, Stars],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <a
      *transloco="let t; prefix: 'recipes.library'"
      class="grid grid-cols-[56px_minmax(0,1fr)] gap-3 rounded-card border-2 border-outline bg-card p-2.5 shadow-control aria-[current=true]:outline-3 aria-[current=true]:outline-offset-2 aria-[current=true]:outline-primary"
      data-testid="recipe-card"
      [routerLink]="path()"
      [attr.aria-current]="current() || null"
    >
      <span
        class="grid size-14 place-items-center rounded-control border-2 border-outline font-display text-xl font-extrabold text-foreground"
        [style.background]="colour()"
        aria-hidden="true"
        data-testid="recipe-tile"
        >{{ initial() }}</span
      >
      <span class="grid min-w-0 content-start gap-0.75">
        <span class="text-[15px] leading-tight font-extrabold" data-testid="recipe-name">{{ recipe().name }}</span>
        <span class="flex flex-wrap items-center gap-x-2.5 gap-y-0.5 text-xs text-muted-foreground">
          @if (recipe().averageRating !== null) {
            <app-stars data-testid="recipe-rating" [value]="recipe().averageRating ?? 0" [label]="t('rating', { rating: ratingText() })" />
          } @else {
            <span data-testid="recipe-rating">{{ t('notRated') }}</span>
          }
          @if (recipe().prepTimeMinutes; as minutes) {
            <span data-testid="recipe-time">{{ prepTime(t, minutes) }}</span>
          }
          <span data-testid="recipe-last-eaten">{{ lastEaten() ?? t('neverEaten') }}</span>
          @if (recipe().nutrition.kcal; as kcal) {
            <span data-testid="recipe-kcal">{{ t('kcal', { kcal }) }}</span>
          }
        </span>
        @if (never() || avoid() || !recipe().makeAgain || tooSoon()) {
          <span class="mt-0.5 flex flex-wrap items-center gap-1">
            @if (never()) {
              <span class="inline-flex items-center gap-1 rounded-full border-[1.5px] border-current bg-danger-surface px-1.75 py-0.5 text-[11px] font-extrabold text-danger" data-testid="badge-never">
                <app-icon name="ban" class="size-3" />{{ t('badge.never') }}
              </span>
            }
            @if (avoid()) {
              <span class="inline-flex items-center rounded-full border-[1.5px] border-current bg-warning-surface px-1.75 py-0.5 text-[11px] font-extrabold text-warning" data-testid="badge-avoid">{{
                t('badge.avoid')
              }}</span>
            }
            @if (!recipe().makeAgain) {
              <span class="inline-flex items-center rounded-full border-[1.5px] border-border px-1.75 py-0.5 text-[11px] font-extrabold text-muted-foreground" data-testid="badge-not-again">{{
                t('badge.notAgain')
              }}</span>
            }
            @if (tooSoon()) {
              <span class="inline-flex items-center gap-1 rounded-full border-[1.5px] border-current bg-secondary px-1.75 py-0.5 text-[11px] font-extrabold text-foreground" data-testid="badge-too-soon">
                <app-icon name="clock" class="size-3" />{{ t('badge.tooSoon') }}
              </span>
            }
          </span>
        }
      </span>
    </a>
  `,
})
export class RecipeCard {
  private readonly language = inject(UiLanguageService);

  readonly recipe = input.required<RecipeListItem>();
  readonly today = input.required<string>();
  readonly current = input(false);

  protected readonly path = computed(() => recipePath(this.recipe().id));
  protected readonly colour = computed(() => recipeTileColour(this.recipe().id));
  protected readonly initial = computed(() => recipeInitial(this.recipe().name));
  protected readonly ratingText = computed(() =>
    new Intl.NumberFormat(this.language.current(), { maximumFractionDigits: 1 }).format(this.recipe().averageRating ?? 0),
  );
  protected readonly lastEaten = computed(() => lastEatenText(this.recipe().cooldown.lastEaten, this.today(), this.language.current()));
  protected readonly never = computed(() => hasDislike(this.recipe(), 'never'));
  protected readonly avoid = computed(() => hasDislike(this.recipe(), 'avoid'));
  protected readonly tooSoon = computed(() => isTooSoon(this.recipe(), this.today()));

  protected prepTime(t: (key: string, params?: Record<string, unknown>) => string, minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    if (hours === 0) {
      return t('minutes', { minutes });
    }
    return rest === 0 ? t('hours', { hours }) : t('hoursMinutes', { hours, minutes: rest });
  }
}
