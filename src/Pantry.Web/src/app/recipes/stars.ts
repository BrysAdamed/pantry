import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export const MAX_STARS = 5;

/** A rating as five stars, rounded to whole ones; `label` says the exact value to screen readers. */
@Component({
  selector: 'app-stars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'inline-flex tracking-[1px] text-warning',
    role: 'img',
    '[attr.aria-label]': 'label()',
  },
  template: `<span aria-hidden="true">{{ filled() }}</span
    ><span class="text-muted-foreground" aria-hidden="true">{{ empty() }}</span>`,
})
export class Stars {
  readonly value = input.required<number>();
  readonly label = input.required<string>();

  private readonly count = computed(() =>
    Math.min(MAX_STARS, Math.max(0, Math.round(this.value()))),
  );
  protected readonly filled = computed(() => '★'.repeat(this.count()));
  protected readonly empty = computed(() => '☆'.repeat(MAX_STARS - this.count()));
}
