import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, linkedSignal, signal, viewChild } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { CatalogApi, Ingredient, IngredientChoice, MergePreview, mergePreviewUrl } from '../../catalog/catalog-api';
import { IngredientPicker } from '../../catalog/ingredient-picker/ingredient-picker';
import { DialogFrame } from '../../shared/dialog/dialogs';
import { Icon } from '../../shared/icon/icon';
import { MAX_ALIASES } from '../ingredient-detail/ingredient-draft';
import { IngredientTexts } from '../ingredient-texts';

/** The ingredient a merge starts from, and the other one when it is already known (a possible duplicate, a name clash). */
export interface MergeDialogData {
  ingredient: Ingredient;
  other?: Ingredient;
}

type MergeError = 'gone' | 'tooManyAliases' | 'failed';

/** The one that stays by default: a confirmed ingredient over one that needs review, otherwise the one the merge started from. */
export function defaultKeep(ingredient: Ingredient, other: Ingredient): string {
  return ingredient.needsReview && !other.needsReview ? other.id : ingredient.id;
}

/**
 * Merging two ingredients (PAN-29, ADR 0020): pick the other one unless it is known, choose which one stays, read the
 * preview of what moves, confirm. Closes with the ingredient that stays.
 */
@Component({
  selector: 'app-merge-dialog',
  imports: [TranslocoDirective, HlmButton, Icon, DialogFrame, IngredientPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './merge-dialog.html',
})
export class MergeDialog {
  private readonly data = inject<MergeDialogData>(DIALOG_DATA);
  private readonly dialogRef = inject<DialogRef<Ingredient>>(DialogRef);
  private readonly api = inject(CatalogApi);
  protected readonly texts = inject(IngredientTexts);

  private readonly keepGroup = viewChild<ElementRef<HTMLElement>>('keepGroup');

  protected readonly ingredient = this.data.ingredient;
  protected readonly excluded = [this.data.ingredient.id];
  protected readonly maxAliases = MAX_ALIASES;
  /** Step 1 (the picker) shows only when the other ingredient was not given; then *Back* returns to it. */
  protected readonly withPicker = !this.data.other;
  protected readonly other = signal<Ingredient | null>(this.data.other ?? null);
  protected readonly keepId = linkedSignal(() => {
    const other = this.other();
    return other ? defaultKeep(this.ingredient, other) : null;
  });
  protected readonly choices = computed(() => {
    const other = this.other();
    return other ? [this.ingredient, other] : [];
  });
  private readonly goId = computed(() => {
    const keep = this.keepId();
    return keep ? this.choices().find((i) => i.id !== keep)!.id : null;
  });

  protected readonly preview = httpResource<MergePreview>(() => {
    const keep = this.keepId();
    const go = this.goId();
    return keep && go ? { url: mergePreviewUrl(keep), params: { sourceId: go } } : undefined;
  });
  /** The preview of the current choice; null while it loads. */
  protected readonly plan = computed(() => {
    const preview = this.preview.hasValue() ? this.preview.value() : null;
    return preview && preview.target.id === this.keepId() && preview.source.id === this.goId() ? preview : null;
  });
  protected readonly lines = computed(() => {
    const plan = this.plan();
    if (!plan) {
      return null;
    }
    const moving = plan.references.filter((r) => r.source > 0).map((r) => ({ module: r.module, count: r.source }));
    return {
      keep: plan.target.name,
      gone: plan.source.name,
      aliases: plan.aliasesToAdd.join(', '),
      moves: this.texts.usage(moving),
      added: this.texts.conversions(plan.conversionsToAdd, plan.target.baseUnit),
      skipped: this.texts.conversions(plan.conversionsSkipped, plan.source.baseUnit),
      keepUnit: this.texts.unit(plan.target.baseUnit),
      goneUnit: this.texts.unit(plan.source.baseUnit),
    };
  });

  protected readonly merging = signal(false);
  private readonly mergeError = signal<MergeError | null>(null);
  protected readonly error = computed<MergeError | null>(() => {
    const failed = this.preview.error();
    if (failed) {
      return failed instanceof HttpErrorResponse && failed.status === HttpStatusCode.NotFound ? 'gone' : 'failed';
    }
    return this.mergeError();
  });
  protected readonly canMerge = computed(() => {
    const plan = this.plan();
    return !!plan && !plan.exceedsAliasLimit && !this.merging();
  });

  protected pick(choice: IngredientChoice): void {
    if (choice.kind === 'existing') {
      this.other.set(choice.ingredient);
      this.mergeError.set(null);
    }
  }

  protected back(): void {
    this.other.set(null);
    this.mergeError.set(null);
  }

  protected keep(id: string): void {
    this.keepId.set(id);
    this.mergeError.set(null);
  }

  /** Arrow keys switch between the two, as in a radio group. */
  protected onKeepKeydown(event: KeyboardEvent): void {
    if (!['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp'].includes(event.key)) {
      return;
    }
    event.preventDefault();
    const index = this.choices().findIndex((i) => i.id !== this.keepId());
    this.keep(this.choices()[index].id);
    this.keepGroup()?.nativeElement.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus();
  }

  /** What one of the two holds: from the preview, so only while it is loaded. */
  protected usageOf(ingredient: Ingredient): string | null {
    const plan = this.plan();
    if (!plan) {
      return null;
    }
    const isTarget = plan.target.id === ingredient.id;
    return this.texts.usage(
      plan.references.map((r) => ({ module: r.module, count: isTarget ? r.target : r.source })).filter((r) => r.count > 0),
    );
  }

  protected async merge(): Promise<void> {
    const plan = this.plan();
    if (!plan || !this.canMerge()) {
      return;
    }
    this.merging.set(true);
    this.mergeError.set(null);
    try {
      this.dialogRef.close(await this.api.mergeIngredients(plan.target.id, plan.source.id));
    } catch (error) {
      const status = error instanceof HttpErrorResponse ? error.status : 0;
      this.mergeError.set(status === HttpStatusCode.NotFound ? 'gone' : status === HttpStatusCode.Conflict ? 'tooManyAliases' : 'failed');
      if (status === HttpStatusCode.Conflict) {
        this.preview.reload();
      }
    } finally {
      this.merging.set(false);
    }
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
