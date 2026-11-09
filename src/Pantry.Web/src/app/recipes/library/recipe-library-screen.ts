import { httpResource } from '@angular/common/http';
import { computed, inject, Injectable, linkedSignal, resource, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import {
  libraryParams,
  RECIPE_TAGS_URL,
  RECIPES_URL,
  RecipeLibraryFilter,
  RecipeListItem,
  RecipeSort,
  RecipesApi,
  RecipesPage,
  TagUsage,
} from '../recipes-api';
import { localDateIso } from '../local-date';

export const RECIPE_LIBRARY_SEARCH_DELAY_MS = 250;
export const RECIPE_LIBRARY_PAGE_SIZE = 50;
export const RECIPE_LIBRARY_TAG_CHIPS = 6;
export const DEFAULT_RECIPE_SORT: RecipeSort = 'rating';

/**
 * State of the recipe library shared by the list and the pane beside it: search, tag and *without never* filters, the
 * archive toggle, the sort, and the pages loaded so far. Provided by `RecipeLibrary`.
 */
@Injectable()
export class RecipeLibraryScreen {
  private readonly api = inject(RecipesApi);

  /** The date *too soon* and *last eaten* are counted from. */
  readonly today = localDateIso();

  readonly searchText = signal('');
  readonly tags = signal<readonly string[]>([]);
  readonly withoutNever = signal(false);
  readonly archived = signal(false);
  readonly sort = signal<RecipeSort>(DEFAULT_RECIPE_SORT);

  readonly search = computed(() => this.searchText().trim().replace(/\s+/g, ' '));
  private readonly appliedSearch = toSignal(
    toObservable(this.search).pipe(debounceTime(RECIPE_LIBRARY_SEARCH_DELAY_MS), distinctUntilChanged()),
    { initialValue: '' },
  );

  private readonly filter = computed<RecipeLibraryFilter>(() => ({
    search: this.appliedSearch() || undefined,
    tags: this.tags(),
    withoutNever: this.withoutNever() || undefined,
    archived: this.archived() || undefined,
    sort: this.sort(),
  }));

  /** How many pages are shown; back to one whenever the filter changes. */
  private readonly pages = linkedSignal({ source: this.filter, computation: () => 1 });

  private readonly page = resource({
    params: () => ({ filter: this.filter(), page: this.pages() }),
    loader: ({ params }) => this.api.libraryPage(params.filter, params.page, RECIPE_LIBRARY_PAGE_SIZE),
  });

  /** Recipes not archived, unfiltered: zero means the library is empty, not that nothing matches. */
  private readonly librarySize = httpResource<RecipesPage>(() => ({ url: RECIPES_URL, params: libraryParams({}, 1, 1) }));

  private readonly popularTags = httpResource<TagUsage[]>(() => ({
    url: RECIPE_TAGS_URL,
    params: { limit: RECIPE_LIBRARY_TAG_CHIPS },
  }));

  /** The pages loaded so far, kept while the next filter loads so the list does not blink while typing. */
  readonly items = linkedSignal<RecipesPage | undefined, RecipeListItem[] | undefined>({
    source: () => (this.page.hasValue() ? this.page.value() : undefined),
    computation: (page, previous) => {
      if (!page) {
        return previous?.value;
      }
      return page.page === 1 ? page.items : [...(previous?.value ?? []), ...page.items];
    },
  });

  readonly matching = computed(() => (this.page.hasValue() ? this.page.value().total : null));
  readonly hasMore = computed(() => (this.items()?.length ?? 0) < (this.matching() ?? 0));
  readonly loadingMore = computed(() => this.page.isLoading() && this.pages() > 1);
  readonly loadFailed = computed(() => !!this.page.error() && this.pages() === 1);
  readonly moreFailed = computed(() => !!this.page.error() && this.pages() > 1);
  readonly libraryEmpty = computed(() => this.librarySize.hasValue() && this.librarySize.value().total === 0);
  readonly libraryKnown = computed(() => this.librarySize.hasValue());

  readonly filtered = computed(() => !!this.search() || this.tags().length > 0 || this.withoutNever() || this.archived());

  /** The most used tags, then any selected tag that is not among them. */
  readonly tagChips = computed(() => {
    const popular = this.popularTags.hasValue() ? this.popularTags.value().map((usage) => usage.tag) : [];
    return [...popular, ...this.tags().filter((tag) => !popular.includes(tag))];
  });

  toggleTag(tag: string): void {
    this.tags.update((tags) => (tags.includes(tag) ? tags.filter((t) => t !== tag) : [...tags, tag]));
  }

  showMore(): void {
    if (this.hasMore() && !this.page.isLoading()) {
      this.pages.update((pages) => pages + 1);
    }
  }

  clearFilters(): void {
    this.searchText.set('');
    this.tags.set([]);
    this.withoutNever.set(false);
    this.archived.set(false);
  }

  /** Reads the library again from the first page after a recipe changed (a rating moves it in the sort). */
  changed(): void {
    if (this.pages() === 1) {
      this.page.reload();
    } else {
      this.pages.set(1);
    }
    this.librarySize.reload();
  }

  retry(): void {
    this.page.reload();
  }
}
