import { inject, Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { CatalogApi, Ingredient } from '../../catalog/catalog-api';
import { Toasts } from '../../core/toast/toasts';
import { Dialogs } from '../../shared/dialog/dialogs';
import { IngredientsScreen } from '../ingredients-screen';
import { MergeDialog, MergeDialogData } from './merge-dialog';

/**
 * Clean-up of duplicates on the Ingredients screen: merging two ingredients and dismissing a possible duplicate.
 * Both refresh the screen and confirm with a toast. Provided by `IngredientsPage`.
 */
@Injectable()
export class IngredientMerges {
  private readonly api = inject(CatalogApi);
  private readonly dialogs = inject(Dialogs);
  private readonly toasts = inject(Toasts);
  private readonly transloco = inject(TranslocoService);
  private readonly screen = inject(IngredientsScreen);

  /** Opens the merge dialog; resolves with the ingredient that stays, or null when nothing was merged. */
  async merge(ingredient: Ingredient, other?: Ingredient): Promise<Ingredient | null> {
    const merged = await this.dialogs.open<Ingredient, MergeDialogData>(MergeDialog, {
      label: this.transloco.translate('ingredients.merge.label'),
      data: { ingredient, other },
      wide: true,
    });
    if (!merged) {
      return null;
    }
    this.screen.changed();
    this.toasts.show(this.transloco.translate('ingredients.toast.merged', { name: merged.name }));
    return merged;
  }

  /** Like `merge`, with the other ingredient known only by id (a name clash); null when it cannot be read. */
  async mergeWithId(ingredient: Ingredient, otherId: string): Promise<Ingredient | null> {
    let other: Ingredient;
    try {
      other = await this.api.ingredient(otherId);
    } catch {
      this.screen.changed();
      this.toasts.show(this.transloco.translate('ingredients.merge.error.gone'));
      return null;
    }
    return this.merge(ingredient, other);
  }

  async markNotDuplicates(one: Ingredient, other: Ingredient): Promise<void> {
    try {
      await this.api.markNotDuplicates(one.id, other.id);
      this.toasts.show(this.transloco.translate('ingredients.toast.notDuplicates'));
    } catch {
      this.toasts.show(this.transloco.translate('ingredients.duplicates.notDuplicatesFailed'));
    }
    this.screen.reloadDuplicates();
  }
}
