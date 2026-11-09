import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { localDateIso } from '../local-date';
import { RatingResult } from '../recipes-api';
import { RatingSheet, RatingSheetData } from './rating-sheet';

const result: RatingResult = {
  entry: { id: 'h1', recipeId: 'curry', date: '2026-11-08', mealSlotId: 'dinner', planEntryId: 'p1', rating: 4 },
  recipe: { recipeId: 'curry', makeAgain: false, averageRating: 4, ratingCount: 1, isFavourite: false },
};

describe('RatingSheet', () => {
  let fixture: ComponentFixture<RatingSheet>;
  let http: HttpTestingController;
  let close: ReturnType<typeof vi.fn>;

  function open(data: Partial<RatingSheetData> = {}): HTMLElement {
    close = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslocoTesting(),
        {
          provide: DIALOG_DATA,
          useValue: {
            entryId: 'h1',
            recipeName: 'Chicken curry',
            date: '2026-11-08',
            mealSlotName: 'Dinner',
            rating: null,
            makeAgain: null,
            ...data,
          } satisfies RatingSheetData,
        },
        { provide: DialogRef, useValue: { close } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(RatingSheet);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const byTestId = (root: HTMLElement, id: string) => root.querySelector<HTMLButtonElement>(`[data-testid="${id}"]`);

  async function click(root: HTMLElement, id: string): Promise<void> {
    byTestId(root, id)!.click();
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('saves stars only once some are chosen, without a make again answer nobody gave', async () => {
    // Arrange
    const root = open();
    expect(byTestId(root, 'rating-when')!.textContent).toContain('8 Nov 2026');
    expect(byTestId(root, 'rating-save')!.disabled).toBe(true);
    expect(byTestId(root, 'rating-remove')).toBeNull();

    // Act
    await click(root, 'star-4');
    await click(root, 'rating-save');
    const request = http.expectOne('/api/recipes/history/h1/rating');
    request.flush(result);
    await fixture.whenStable();

    // Assert
    expect(byTestId(root, 'star-word')!.textContent!.trim()).toBe('Very good');
    expect(byTestId(root, 'star-4')!.getAttribute('aria-checked')).toBe('true');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ stars: 4 });
    expect(close).toHaveBeenCalledWith({ kind: 'rated', result });
  });

  it('sends the make again answer and says what no means', async () => {
    // Arrange
    const root = open({ makeAgain: true });
    expect(byTestId(root, 'make-again-yes')!.getAttribute('aria-pressed')).toBe('true');

    // Act
    await click(root, 'make-again-no');
    await click(root, 'star-2');
    await click(root, 'rating-save');
    const request = http.expectOne('/api/recipes/history/h1/rating');
    request.flush(result);
    await fixture.whenStable();

    // Assert
    expect(byTestId(root, 'make-again-hint')).not.toBeNull();
    expect(request.request.body).toEqual({ stars: 2, makeAgain: false });
  });

  it('changes or removes a rating that is there', async () => {
    // Arrange
    const root = open({ rating: 5, date: localDateIso(-1) });
    expect(byTestId(root, 'rating-when')!.textContent).toContain('Yesterday');
    expect(byTestId(root, 'star-5')!.getAttribute('aria-checked')).toBe('true');
    expect(byTestId(root, 'rating-save')!.disabled).toBe(false);

    // Act
    await click(root, 'rating-remove');
    const request = http.expectOne('/api/recipes/history/h1/rating');
    request.flush({ ...result, entry: { ...result.entry, rating: null } });
    await fixture.whenStable();

    // Assert
    expect(request.request.method).toBe('DELETE');
    expect(close).toHaveBeenCalledWith(expect.objectContaining({ kind: 'removed' }));
  });

  it('keeps the sheet open with a message when saving fails', async () => {
    // Arrange
    const root = open();

    // Act
    await click(root, 'star-3');
    await click(root, 'rating-save');
    http.expectOne('/api/recipes/history/h1/rating').flush({}, { status: 404, statusText: 'Not Found' });
    await fixture.whenStable();
    fixture.detectChanges();

    // Assert
    expect(byTestId(root, 'rating-error')!.textContent).toContain('no longer in the history');
    expect(close).not.toHaveBeenCalled();
  });

  it('closes without a result for later', async () => {
    // Arrange
    const root = open();

    // Act
    await click(root, 'rating-later');

    // Assert
    expect(close).toHaveBeenCalledWith();
  });
});
