import { inject, Injectable } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Toasts } from '../../core/toast/toasts';
import { Dialogs } from '../../shared/dialog/dialogs';
import { RatingOutcome, RatingSheet, RatingSheetData } from './rating-sheet';

/**
 * Asks the household to rate a history entry: the rating sheet, a bottom sheet on phones and a small dialog on
 * desktop, then a toast. Used by *to rate*, the recipe history and the planner when an entry is marked eaten.
 */
@Injectable({ providedIn: 'root' })
export class RatingPrompt {
  private readonly dialogs = inject(Dialogs);
  private readonly toasts = inject(Toasts);
  private readonly transloco = inject(TranslocoService);

  /** Resolves with what changed, or null when the person chose *later* or closed the sheet. */
  async open(data: RatingSheetData): Promise<RatingOutcome | null> {
    const outcome = await this.dialogs.open<RatingOutcome, RatingSheetData>(RatingSheet, {
      label: this.transloco.translate('recipes.rating.dialogLabel', { name: data.recipeName }),
      data,
    });
    if (!outcome) {
      return null;
    }
    this.toasts.show(
      this.transloco.translate(outcome.kind === 'rated' ? 'recipes.rating.toast.rated' : 'recipes.rating.toast.removed'),
    );
    return outcome;
  }
}
