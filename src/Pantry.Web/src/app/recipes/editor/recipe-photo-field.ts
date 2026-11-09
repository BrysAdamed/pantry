import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { RECIPE_PHOTO_LIMITS, RecipePhoto } from '../recipes-api';
import { PhotoChange, PhotoProblem, photoProblemOf, removedPhoto, KEEP_PHOTO } from './recipe-photo';

/**
 * The photo of the recipe in the editor: drop a picture or tap to choose one, replace or remove it. Nothing is sent
 * until the recipe saves; the editor applies `change` then.
 */
@Component({
  selector: 'app-recipe-photo-field',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid gap-1.5' },
  template: `
    <ng-container *transloco="let t; prefix: 'recipes.editor.photo'">
      <span class="text-[13px] font-bold" id="recipe-photo-label">{{ t('label') }}</span>
      @if (shown(); as src) {
        <img
          class="aspect-4/3 w-full rounded-card border-2 border-outline bg-muted object-cover"
          data-testid="recipe-photo-preview"
          [src]="src"
          [alt]="preview() ? t('newAlt') : t('alt', { name: name() })"
        />
        <div class="flex flex-wrap items-center gap-2">
          <label hlmBtn variant="outline" size="sm" class="cursor-pointer focus-within:outline-3 focus-within:outline-primary">
            <input
              type="file"
              class="sr-only"
              data-testid="recipe-photo-input"
              [accept]="accept"
              (change)="picked($event)"
            />
            {{ t('replace') }}
          </label>
          <button hlmBtn variant="ghost" size="sm" type="button" data-testid="recipe-photo-remove" (click)="remove()">
            {{ t('remove') }}
          </button>
          @if (preview()) {
            <span class="text-xs text-muted-foreground">{{ t('pending') }}</span>
          }
        </div>
      } @else {
        <label
          class="grid min-h-28 cursor-pointer place-items-center content-center gap-1.5 rounded-card border-2 border-dashed border-border p-3 text-center text-[13px] text-muted-foreground focus-within:outline-3 focus-within:outline-primary data-[over=true]:border-primary data-[over=true]:bg-accent"
          data-testid="recipe-photo-drop"
          [attr.data-over]="over()"
          (dragenter)="dragOver($event)"
          (dragover)="dragOver($event)"
          (dragleave)="over.set(false)"
          (drop)="dropped($event)"
        >
          <input
            type="file"
            class="sr-only"
            aria-labelledby="recipe-photo-label"
            data-testid="recipe-photo-input"
            [accept]="accept"
            (change)="picked($event)"
          />
          <svg viewBox="0 0 24 24" class="size-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="3" y="4" width="18" height="16" rx="2" />
            <circle cx="9" cy="10" r="2" />
            <path d="M21 16l-5-5-9 9" />
          </svg>
          <span class="font-bold text-foreground">{{ over() ? t('dropping') : t('drop') }}</span>
          <span class="text-xs">{{ t('optional') }}</span>
        </label>
        @if (change().kind === 'remove') {
          <span class="flex flex-wrap items-center gap-2 text-xs text-muted-foreground" data-testid="recipe-photo-removed">
            {{ t('removed') }}
            <button type="button" class="font-bold text-primary hover:underline" data-testid="recipe-photo-undo" (click)="changed.emit(keep)">
              {{ t('undo') }}
            </button>
          </span>
        }
      }
      @if (problem(); as p) {
        <span class="text-xs font-bold text-danger" role="alert" data-testid="recipe-photo-problem">{{ t('problem.' + p) }}</span>
      }
    </ng-container>
  `,
})
export class RecipePhotoField {
  /** The saved photo; null for a new recipe or one without a photo. */
  readonly current = input<RecipePhoto | null>(null);
  readonly change = input.required<PhotoChange>();
  /** The recipe name, for the photo's text alternative. */
  readonly name = input('');
  readonly changed = output<PhotoChange>();

  protected readonly accept = RECIPE_PHOTO_LIMITS.types.join(',');
  protected readonly keep = KEEP_PHOTO;
  protected readonly over = signal(false);
  protected readonly problem = signal<PhotoProblem | null>(null);
  protected readonly preview = signal<string | null>(null);
  protected readonly shown = computed(() => {
    const change = this.change();
    if (change.kind === 'set') {
      return this.preview();
    }
    return change.kind === 'keep' ? (this.current()?.detail ?? null) : null;
  });

  constructor() {
    effect((onCleanup) => {
      const change = this.change();
      if (change.kind !== 'set') {
        this.preview.set(null);
        return;
      }
      const url = URL.createObjectURL(change.file);
      this.preview.set(url);
      onCleanup(() => URL.revokeObjectURL(url));
    });
  }

  protected picked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) {
      this.choose(file);
    }
  }

  protected dragOver(event: DragEvent): void {
    event.preventDefault();
    this.over.set(true);
  }

  protected dropped(event: DragEvent): void {
    event.preventDefault();
    this.over.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) {
      this.choose(file);
    }
  }

  protected remove(): void {
    this.problem.set(null);
    this.changed.emit(removedPhoto(!!this.current()));
  }

  private choose(file: File): void {
    const problem = photoProblemOf(file);
    this.problem.set(problem);
    if (!problem) {
      this.changed.emit({ kind: 'set', file });
    }
  }
}
