import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { CatalogApi, IngredientChoice } from '../../catalog/catalog-api';
import { IngredientPicker } from '../../catalog/ingredient-picker/ingredient-picker';
import { Icon, IconName } from '../../shared/icon/icon';
import { Dislike, DISLIKE_KINDS, DISLIKE_REASON_MAX_LENGTH, DislikeKind, DISLIKES_URL, existingDislikeOf, HouseholdApi } from '../household-api';

interface Column {
  readonly kind: DislikeKind;
  readonly other: DislikeKind;
  readonly icon: IconName;
  readonly card: string;
  readonly title: string;
}

const COLUMNS: readonly Column[] = [
  {
    kind: 'never',
    other: 'avoid',
    icon: 'ban',
    card: 'border-dislike-never shadow-[var(--offset)_var(--offset)_0_var(--color-dislike-never)]',
    title: 'text-dislike-never',
  },
  {
    kind: 'avoid',
    other: 'never',
    icon: 'alert',
    card: 'border-dislike-avoid shadow-[var(--offset)_var(--offset)_0_var(--color-dislike-avoid)]',
    title: 'text-dislike-avoid',
  },
];

/**
 * Household dislikes in two columns, never and avoid. An ingredient is chosen with the shared picker; a new name joins
 * the catalog (needing review) only when the dislike is added. A dislike moves to the other column or is removed.
 */
@Component({
  selector: 'app-dislikes-section',
  imports: [TranslocoDirective, HlmButton, Icon, IngredientPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './dislikes-section.html',
})
export class DislikesSection {
  private readonly api = inject(HouseholdApi);
  private readonly catalog = inject(CatalogApi);
  private writes: Promise<unknown> = Promise.resolve();

  protected readonly columns = COLUMNS;
  protected readonly reasonMaxLength = DISLIKE_REASON_MAX_LENGTH;

  protected readonly resource = httpResource<Dislike[]>(() => DISLIKES_URL);
  protected readonly dislikes = linkedSignal<Dislike[]>(() => this.resource.value() ?? []);
  protected readonly byKind = computed(() =>
    Object.fromEntries(DISLIKE_KINDS.map((kind) => [kind, this.dislikes().filter((d) => d.kind === kind)])) as Record<DislikeKind, Dislike[]>,
  );
  protected readonly taken = computed(() => this.dislikes().map((d) => d.ingredientId));

  protected readonly chosen = signal<Record<DislikeKind, IngredientChoice | null>>({ never: null, avoid: null });
  protected readonly reason = signal('');
  protected readonly adding = signal<DislikeKind | null>(null);
  protected readonly saveFailed = signal(false);
  protected readonly alreadyListed = signal(false);

  protected choose(kind: DislikeKind, choice: IngredientChoice | null): void {
    this.chosen.update((chosen) => ({ ...chosen, [kind]: choice }));
  }

  protected choiceName(choice: IngredientChoice): string {
    return choice.kind === 'existing' ? choice.ingredient.name : choice.name;
  }

  protected async add(event: Event, kind: DislikeKind): Promise<void> {
    event.preventDefault();
    const choice = this.chosen()[kind];
    if (!choice || this.adding()) {
      return;
    }
    this.adding.set(kind);
    this.alreadyListed.set(false);
    try {
      await this.writes;
      const ingredient = await this.catalog.ingredientOf(choice);
      const dislike = await this.api.addDislike(ingredient.id, { kind, reason: kind === 'never' ? reasonOf(this.reason()) : null });
      this.dislikes.update((dislikes) => [...dislikes, dislike]);
      this.choose(kind, null);
      if (kind === 'never') {
        this.reason.set('');
      }
      this.saveFailed.set(false);
    } catch (error) {
      if (existingDislikeOf(error)) {
        this.choose(kind, null);
        this.alreadyListed.set(true);
        this.resource.reload();
      } else {
        this.failAndReload();
      }
    } finally {
      this.adding.set(null);
    }
  }

  protected move(dislike: Dislike, kind: DislikeKind): void {
    const change = { kind, reason: kind === 'never' ? dislike.reason : null };
    this.dislikes.update((dislikes) => [...dislikes.filter((d) => d.id !== dislike.id), { ...dislike, ...change }]);
    this.write(() => this.api.changeDislike(dislike.id, change));
  }

  protected remove(dislike: Dislike): void {
    this.dislikes.update((dislikes) => dislikes.filter((d) => d.id !== dislike.id));
    this.write(() => this.api.deleteDislike(dislike.id));
  }

  /** Shows changes at once and sends them one after another, so the server sees them in the order they were made. */
  private write(send: () => Promise<unknown>): void {
    this.writes = this.writes
      .then(send)
      .then(() => this.saveFailed.set(false))
      .catch(() => this.failAndReload());
  }

  private failAndReload(): void {
    this.saveFailed.set(true);
    this.resource.reload();
  }
}

function reasonOf(value: string): string | null {
  return value.trim() || null;
}
