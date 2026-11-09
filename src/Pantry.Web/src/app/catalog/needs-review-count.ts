import { httpResource } from '@angular/common/http';
import { computed, Injectable } from '@angular/core';
import { filterParams, IngredientsPage, INGREDIENTS_URL } from './catalog-api';

/**
 * How many ingredients need review, for the badge on the Ingredients destination and the "Tidy up" card. Screens
 * that confirm or delete ingredients call `reload()`.
 */
@Injectable({ providedIn: 'root' })
export class NeedsReviewCount {
  private readonly page = httpResource<IngredientsPage>(() => ({
    url: INGREDIENTS_URL,
    params: filterParams({ needsReview: true }, 1, 1),
  }));

  readonly count = computed(() => (this.page.hasValue() ? this.page.value().total : 0));

  reload(): void {
    this.page.reload();
  }
}
