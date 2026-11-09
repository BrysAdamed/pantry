import { NgTemplateOutlet } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { Icon } from '../../shared/icon/icon';
import { Ingredient, INGREDIENT_NAME_MAX_LENGTH, IngredientChoice, IngredientsPage, INGREDIENTS_URL } from '../catalog-api';
import { cleanIngredientName, findInName, ingredientNameKey, markMatch, TextPart } from '../ingredient-name';

export const INGREDIENT_SEARCH_DELAY_MS = 200;
export const INGREDIENT_PICKER_RESULTS = 8;

type PickerOption =
  | {
      readonly kind: 'existing';
      readonly id: string;
      readonly ingredient: Ingredient;
      readonly name: readonly TextPart[];
      readonly alias: readonly TextPart[] | null;
      readonly taken: boolean;
    }
  | { readonly kind: 'new'; readonly id: string; readonly name: string };

let nextId = 0;

/**
 * Search-as-you-type over ingredient names and aliases (ARIA combobox). Emits the chosen ingredient, or, when
 * `allowNew` is on, a new name the host resolves with `CatalogApi.ingredientOf` when its form saves.
 */
@Component({
  selector: 'app-ingredient-picker',
  imports: [TranslocoDirective, Icon, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative block', '(focusout)': 'onFocusOut($event)' },
  templateUrl: './ingredient-picker.html',
})
export class IngredientPicker {
  readonly label = input.required<string>();
  readonly placeholder = input<string | null>(null);
  readonly allowNew = input(true);
  /** Ingredients the host already has: shown, but not selectable. */
  readonly taken = input<readonly string[]>([]);
  /** Ingredients left out of the results, such as the one being merged. */
  readonly exclude = input<readonly string[]>([]);
  readonly picked = output<IngredientChoice>();

  protected readonly listId = `ingredient-picker-${nextId++}`;
  protected readonly maxLength = INGREDIENT_NAME_MAX_LENGTH;

  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('field');

  protected readonly text = signal('');
  protected readonly open = signal(false);
  protected readonly active = signal(0);

  private readonly term = computed(() => cleanIngredientName(this.text()));
  private readonly searchTerm = toSignal(
    toObservable(this.term).pipe(debounceTime(INGREDIENT_SEARCH_DELAY_MS), distinctUntilChanged()),
    { initialValue: '' },
  );

  protected readonly results = httpResource<IngredientsPage>(() => {
    const search = this.searchTerm();
    return search && ingredientNameKey(search)
      ? { url: INGREDIENTS_URL, params: { search, pageSize: INGREDIENT_PICKER_RESULTS + this.exclude().length } }
      : undefined;
  });

  protected readonly options = computed<PickerOption[]>(() => {
    const term = this.term();
    if (!ingredientNameKey(term) || !this.results.hasValue()) {
      return [];
    }
    const excluded = new Set(this.exclude());
    const taken = new Set(this.taken());
    const found = this.results
      .value()
      .items.filter((ingredient) => !excluded.has(ingredient.id))
      .slice(0, INGREDIENT_PICKER_RESULTS)
      .map((ingredient, index): PickerOption => this.existingOption(ingredient, term, taken.has(ingredient.id), index));
    const key = ingredientNameKey(term);
    const known = this.results.value().items.some((i) => [i.name, ...i.aliases].some((n) => ingredientNameKey(n) === key));
    return this.allowNew() && !known ? [...found, { kind: 'new', id: `${this.listId}-new`, name: term }] : found;
  });

  protected readonly expanded = computed(
    () => this.open() && !!ingredientNameKey(this.term()) && (this.results.hasValue() || !!this.results.error()),
  );
  protected readonly activeOption = computed(() => {
    const options = this.options();
    return this.expanded() && options.length ? options[Math.min(this.active(), options.length - 1)] : undefined;
  });

  protected onInput(value: string): void {
    this.text.set(value);
    this.open.set(true);
    this.active.set(0);
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.options().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (this.expanded()) {
          this.active.set(count ? (this.activeIndex() + 1) % count : 0);
        }
        this.open.set(true);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.active.set(count ? (this.activeIndex() - 1 + count) % count : 0);
        break;
      case 'Enter': {
        const option = this.activeOption();
        if (option) {
          event.preventDefault();
          this.choose(option);
        }
        break;
      }
      case 'Escape':
        if (this.expanded()) {
          event.preventDefault();
          event.stopPropagation();
          this.open.set(false);
        }
        break;
    }
  }

  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (!next || !(event.currentTarget as HTMLElement).contains(next)) {
      this.open.set(false);
    }
  }

  protected choose(option: PickerOption): void {
    if (option.kind === 'existing' && option.taken) {
      return;
    }
    this.picked.emit(
      option.kind === 'existing' ? { kind: 'existing', ingredient: option.ingredient } : { kind: 'new', name: option.name },
    );
    this.text.set('');
    this.open.set(false);
    this.active.set(0);
    this.input().nativeElement.focus();
  }

  private activeIndex(): number {
    const option = this.activeOption();
    return option ? this.options().indexOf(option) : 0;
  }

  private existingOption(ingredient: Ingredient, term: string, taken: boolean, index: number): PickerOption {
    const nameMatch = findInName(ingredient.name, term);
    const alias = nameMatch ? null : ingredient.aliases.find((a) => findInName(a, term));
    return {
      kind: 'existing',
      id: `${this.listId}-${index}`,
      ingredient,
      name: markMatch(ingredient.name, term),
      alias: alias ? markMatch(alias, term) : null,
      taken,
    };
  }
}

