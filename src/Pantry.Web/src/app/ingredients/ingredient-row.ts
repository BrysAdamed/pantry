import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { Ingredient } from '../catalog/catalog-api';
import { findInName, markMatch } from '../catalog/ingredient-name';

/** One ingredient in the list: name with the searched part marked, the matched alias, base unit and badges. */
@Component({
  selector: 'app-ingredient-row',
  imports: [RouterLink, TranslocoDirective, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <a
      *transloco="let t"
      class="grid min-h-13 grid-cols-[minmax(0,1fr)_auto] items-center gap-x-2.5 gap-y-1 rounded-control border-2 border-transparent bg-card px-3 py-2.25 hover:border-border aria-[current=true]:border-outline aria-[current=true]:shadow-control"
      data-testid="ingredient-row"
      [routerLink]="['/ingredients', ingredient().id]"
      [attr.aria-current]="current() || null"
    >
      <span class="text-[15px] leading-tight font-extrabold" data-testid="ingredient-name">
        <ng-container *ngTemplateOutlet="marked; context: { $implicit: name() }" />
      </span>
      <span
        class="min-w-9.5 rounded-chip border-[1.5px] border-border px-2 py-0.5 text-center text-xs font-extrabold text-muted-foreground tabular-nums"
        [title]="t('ingredients.row.baseUnit')"
        >{{ t('catalog.unit.' + ingredient().baseUnit) }}</span
      >
      @if (alias() || ingredient().needsReview || ingredient().isStaple) {
        <span class="col-span-full flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
          @if (alias(); as parts) {
            <span data-testid="alias-match">{{ t('catalog.picker.aliasMatch') }} <ng-container *ngTemplateOutlet="marked; context: { $implicit: parts }" /></span>
          }
          @if (ingredient().needsReview) {
            <span
              class="inline-flex items-center rounded-full border-[1.5px] border-current bg-ingredient-review-surface px-1.75 py-0.5 text-[11px] font-extrabold whitespace-nowrap text-ingredient-review"
              data-testid="badge-needs-review"
              >{{ t('ingredients.badge.needsReview') }}</span
            >
          }
          @if (ingredient().isStaple) {
            <span
              class="inline-flex items-center rounded-full border-[1.5px] border-border bg-secondary px-1.75 py-0.5 text-[11px] font-extrabold whitespace-nowrap text-foreground"
              data-testid="badge-staple"
              >{{ t('ingredients.badge.staple') }}</span
            >
          }
        </span>
      }
    </a>

    <ng-template #marked let-parts>
      @for (part of parts; track $index) {
        @if (part.matched) {
          <mark class="rounded-sm bg-accent/45 px-px text-inherit">{{ part.text }}</mark>
        } @else {
          <ng-container>{{ part.text }}</ng-container>
        }
      }
    </ng-template>
  `,
})
export class IngredientRow {
  readonly ingredient = input.required<Ingredient>();
  readonly search = input('');
  readonly current = input(false);

  protected readonly name = computed(() => markMatch(this.ingredient().name, this.search()));
  protected readonly alias = computed(() => {
    const search = this.search();
    if (!search || findInName(this.ingredient().name, search)) {
      return null;
    }
    const alias = this.ingredient().aliases.find((a) => findInName(a, search));
    return alias ? markMatch(alias, search) : null;
  });
}
