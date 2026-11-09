import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { DialogFrame } from '../../shared/dialog/dialogs';
import { Icon } from '../../shared/icon/icon';
import { nearDayOf, RECIPE_DATE_FORMAT } from '../local-date';
import { RatingResult, RecipesApi } from '../recipes-api';
import { MAX_STARS } from '../stars';

/** The history entry to rate and what is known about it. */
export interface RatingSheetData {
  entryId: string;
  recipeName: string;
  /** `yyyy-MM-dd`. */
  date: string;
  mealSlotName: string | null;
  /** The stars it has now; null while not rated. */
  rating: number | null;
  /** The answer to *make again?* to start with; null leaves it unanswered, so saving keeps the recipe's flag. */
  makeAgain: boolean | null;
}

/** What the sheet did: the entry was rated (added or changed) or its rating removed. */
export interface RatingOutcome {
  kind: 'rated' | 'removed';
  result: RatingResult;
}

type RatingError = 'gone' | 'failed';

/**
 * Rating one history entry after a meal (prototype `recipes.html`): stars with words, *make again?*, *later* and
 * *save*; *remove* for an entry that is rated already. Writes the rating itself and closes with a `RatingOutcome`, or
 * with nothing for *later*. Open it through `RatingPrompt`.
 */
@Component({
  selector: 'app-rating-sheet',
  imports: [TranslocoDirective, HlmButton, Icon, DialogFrame, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './rating-sheet.html',
})
export class RatingSheet {
  protected readonly data = inject<RatingSheetData>(DIALOG_DATA);
  private readonly dialogRef = inject<DialogRef<RatingOutcome>>(DialogRef);
  private readonly api = inject(RecipesApi);
  protected readonly language = inject(UiLanguageService).current;

  protected readonly dateFormat = RECIPE_DATE_FORMAT;
  protected readonly starValues = Array.from({ length: MAX_STARS }, (_, i) => i + 1);
  protected readonly nearDay = nearDayOf(this.data.date);
  protected readonly isRated = this.data.rating !== null;

  protected readonly stars = signal(this.data.rating ?? 0);
  protected readonly makeAgain = signal(this.data.makeAgain);
  protected readonly busy = signal(false);
  protected readonly error = signal<RatingError | null>(null);
  protected readonly canSave = computed(() => this.stars() > 0 && !this.busy());

  protected pick(stars: number): void {
    this.stars.set(stars);
  }

  protected answer(makeAgain: boolean): void {
    this.makeAgain.set(makeAgain);
  }

  protected later(): void {
    this.dialogRef.close();
  }

  protected async save(): Promise<void> {
    if (!this.canSave()) {
      return;
    }
    await this.write('rated', () => this.api.rate(this.data.entryId, this.stars(), this.makeAgain()));
  }

  protected async remove(): Promise<void> {
    if (this.busy()) {
      return;
    }
    await this.write('removed', () => this.api.removeRating(this.data.entryId));
  }

  private async write(kind: RatingOutcome['kind'], request: () => Promise<RatingResult>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.dialogRef.close({ kind, result: await request() });
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound ? 'gone' : 'failed',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
