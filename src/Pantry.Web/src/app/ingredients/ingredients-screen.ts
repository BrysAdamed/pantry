import { httpResource } from '@angular/common/http';
import { computed, inject, Injectable, linkedSignal, resource, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import {
  CatalogApi,
  filterParams,
  Ingredient,
  INGREDIENT_CATEGORIES,
  IngredientCategory,
  IngredientFilter,
  IngredientsPage,
  INGREDIENTS_URL,
} from '../catalog/catalog-api';
import { cleanIngredientName, ingredientNameKey } from '../catalog/ingredient-name';
import { NeedsReviewCount } from '../catalog/needs-review-count';

export const INGREDIENT_LIST_SEARCH_DELAY_MS = 250;

export interface IngredientGroup {
  readonly category: IngredientCategory;
  readonly items: readonly Ingredient[];
}

/**
 * State of the Ingredients screen shared by the list and the open ingredient: filters, the filtered catalog grouped
 * by category, and the counts. Provided by `IngredientsPage`; call `changed()` after a write so everything reloads.
 */
@Injectable()
export class IngredientsScreen {
  private readonly api = inject(CatalogApi);
  private readonly reviewCount = inject(NeedsReviewCount);

  readonly searchText = signal('');
  readonly needsReviewOnly = signal(false);
  readonly staplesOnly = signal(false);
  readonly category = signal<IngredientCategory | null>(null);

  /** The typed search, used for highlighting at once; the list follows after a short pause. */
  readonly search = computed(() => cleanIngredientName(this.searchText()));
  private readonly appliedSearch = toSignal(
    toObservable(this.search).pipe(debounceTime(INGREDIENT_LIST_SEARCH_DELAY_MS), distinctUntilChanged()),
    { initialValue: '' },
  );

  private readonly filter = computed<IngredientFilter>(() => ({
    search: ingredientNameKey(this.appliedSearch()) ? this.appliedSearch() : undefined,
    needsReview: this.needsReviewOnly() || undefined,
    staple: this.staplesOnly() || undefined,
    category: this.category() ?? undefined,
  }));

  private readonly list = resource({
    params: () => this.filter(),
    loader: ({ params }) => this.api.allIngredients(params),
  });

  private readonly catalogSize = httpResource<IngredientsPage>(() => ({ url: INGREDIENTS_URL, params: filterParams({}, 1, 1) }));

  /** The last loaded list, kept while the next one loads so the list does not blink while typing. */
  readonly items = linkedSignal<Ingredient[] | undefined, Ingredient[] | undefined>({
    source: () => (this.list.hasValue() ? this.list.value() : undefined),
    computation: (items, previous) => items ?? previous?.value,
  });

  readonly loadFailed = computed(() => !!this.list.error() && !this.items());
  readonly total = computed(() => (this.catalogSize.hasValue() ? this.catalogSize.value().total : null));
  readonly catalogEmpty = computed(() => this.total() === 0);
  readonly needsReviewCount = this.reviewCount.count;

  readonly filtered = computed(() => !!ingredientNameKey(this.search()) || this.needsReviewOnly() || this.staplesOnly() || !!this.category());

  readonly groups = computed<IngredientGroup[]>(() => {
    const items = this.items() ?? [];
    return INGREDIENT_CATEGORIES.map((category) => ({ category, items: items.filter((i) => i.category === category) })).filter(
      (group) => group.items.length > 0,
    );
  });

  /** The ingredients in the order the list shows them. */
  readonly ordered = computed(() => this.groups().flatMap((group) => group.items));

  clearFilters(): void {
    this.searchText.set('');
    this.needsReviewOnly.set(false);
    this.staplesOnly.set(false);
    this.category.set(null);
  }

  /** The next ingredient to review after `id` in list order, wrapping around; null when it was the last one. */
  nextToReview(id: string): string | null {
    const queue = this.ordered().filter((i) => i.needsReview);
    const at = queue.findIndex((i) => i.id === id);
    const next = [...queue.slice(at + 1), ...queue.slice(0, Math.max(at, 0))].find((i) => i.id !== id);
    return next?.id ?? null;
  }

  /** Reads the list and the counts again after an ingredient was added, changed, confirmed or deleted. */
  changed(): void {
    this.list.reload();
    this.catalogSize.reload();
    this.reviewCount.reload();
  }

  retry(): void {
    this.list.reload();
  }
}
