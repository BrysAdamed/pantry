import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
  TemplateRef,
  untracked,
  viewChild,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { IngredientChoice } from '../../catalog/catalog-api';
import { IngredientPicker } from '../../catalog/ingredient-picker/ingredient-picker';
import { UNITS } from '../../catalog/units';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { Toasts } from '../../core/toast/toasts';
import { Dislike, DISLIKES_URL } from '../../household/household-api';
import { DialogContext, DialogFrame, Dialogs } from '../../shared/dialog/dialogs';
import { Icon } from '../../shared/icon/icon';
import { Switch } from '../../shared/switch/switch';
import {
  Recipe,
  RECIPE_LIMITS,
  RECIPE_TAGS_URL,
  recipeUrl,
  RecipesApi,
  TagUsage,
} from '../recipes-api';
import { RECIPES_PATH, recipePath } from '../recipes-paths';
import {
  addLine,
  addSection,
  addStep,
  addTag,
  changeLine,
  changeOf,
  changeStep,
  draftOf,
  GroupDraft,
  LineDraft,
  moveLine,
  moveLineToGroup,
  moveSection,
  moveStep,
  newDraft,
  Nutrient,
  NUTRIENTS,
  pickIngredient,
  problemsOf,
  RecipeDraft,
  removeLine,
  removeSection,
  removeStep,
  removeTag,
  renameSection,
  sameDraft,
  TagProblem,
} from './recipe-draft';
import { KEEP_PHOTO, PhotoChange } from './recipe-photo';
import { RecipePhotoField } from './recipe-photo-field';

export const TAG_SEARCH_DELAY_MS = 200;
const TAG_SUGGESTIONS = 8;

type SaveError = 'failed' | 'invalid';

/** What the editor says under a line while it is typed: a household dislike, or a name the catalog does not have yet. */
export type LineHint =
  { kind: 'never'; reason: string | null } | { kind: 'avoid' } | { kind: 'new' };

/**
 * The recipe editor for a new recipe (`/recipes/new`) and an existing one (`/recipes/:id/edit`): quantities typed for
 * the recipe servings, ingredient lines with the shared picker and optional sections, steps, tags with suggestions,
 * prep time, source, photo, nutrition per serving, cooldown and make again. Lines with a *never* or *avoid* ingredient are
 * marked while typing. Leaving with unsaved changes asks first (`confirmDiscardRecipe`).
 */
@Component({
  selector: 'app-recipe-editor',
  imports: [
    TranslocoDirective,
    RouterLink,
    HlmButton,
    Icon,
    Switch,
    DialogFrame,
    IngredientPicker,
    RecipePhotoField,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5', '(window:beforeunload)': 'onBeforeUnload($event)' },
  templateUrl: './recipe-editor.html',
})
export class RecipeEditor {
  /** The route's `:id`; absent for a new recipe. */
  readonly id = input<string>();

  private readonly api = inject(RecipesApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(Toasts);
  private readonly dialogs = inject(Dialogs);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(UiLanguageService).current;

  protected readonly units = UNITS;
  protected readonly nutrients = NUTRIENTS;
  protected readonly limits = RECIPE_LIMITS;

  private readonly discardDialog =
    viewChild.required<TemplateRef<DialogContext<string | null, boolean>>>('discardDialog');

  protected readonly isNew = computed(() => !this.id());
  /** Back and cancel go to the recipe, or to the library for a new one. */
  protected readonly backPath = computed(() => (this.id() ? recipePath(this.id()!) : [RECIPES_PATH]));
  protected readonly recipe = httpResource<Recipe>(() =>
    this.id() ? recipeUrl(this.id()!) : undefined,
  );
  protected readonly saved = computed(() => (this.recipe.hasValue() ? this.recipe.value() : null));
  protected readonly gone = computed(() => {
    const error = this.recipe.error();
    return error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound;
  });

  private readonly initial = computed<RecipeDraft | null>(() => {
    if (this.isNew()) {
      return newDraft();
    }
    const saved = this.saved();
    return saved
      ? draftOf(
          saved,
          untracked(() => decimalSeparatorOf(this.language())),
        )
      : null;
  });

  protected readonly draft = linkedSignal(() => this.initial());
  protected readonly dirty = computed(() => {
    const draft = this.draft();
    const initial = this.initial();
    return (!!draft && !!initial && !sameDraft(draft, initial)) || this.photoChange().kind !== 'keep';
  });
  protected readonly problems = computed(() => {
    const draft = this.draft();
    return draft ? problemsOf(draft) : new Map();
  });
  /** Problems show once a save was tried, so a half-typed form is not red all over. */
  protected readonly showProblems = linkedSignal<string | undefined, boolean>({
    source: this.id,
    computation: () => false,
  });
  /** Applied after the recipe itself saves, because a new recipe has no id before that. */
  protected readonly photoChange = linkedSignal<string | undefined, PhotoChange>({
    source: this.id,
    computation: () => KEEP_PHOTO,
  });
  protected readonly saving = signal(false);
  protected readonly saveError = linkedSignal<string | undefined, SaveError | null>({
    source: this.id,
    computation: () => null,
  });
  private leaveWithoutAsking = false;

  private readonly dislikes = httpResource<Dislike[]>(() => DISLIKES_URL);
  private readonly dislikeByIngredient = computed(
    () => new Map((this.dislikes.value() ?? []).map((d) => [d.ingredientId, d])),
  );
  protected readonly sections = computed(() => this.draft()?.groups.slice(1) ?? []);

  protected readonly tagText = linkedSignal<string | undefined, string>({
    source: this.id,
    computation: () => '',
  });
  protected readonly tagProblem = linkedSignal<string | undefined, TagProblem | null>({
    source: this.id,
    computation: () => null,
  });
  private readonly tagSearch = toSignal(
    toObservable(this.tagText).pipe(debounceTime(TAG_SEARCH_DELAY_MS), distinctUntilChanged()),
    {
      initialValue: '',
    },
  );
  private readonly tagUsage = httpResource<TagUsage[]>(() => ({
    url: RECIPE_TAGS_URL,
    params: { search: this.tagSearch().trim(), limit: TAG_SUGGESTIONS + RECIPE_LIMITS.maxTags },
  }));
  protected readonly tagSuggestions = computed(() => {
    const taken = new Set(this.draft()?.tags ?? []);
    return (this.tagUsage.value() ?? [])
      .map((t) => t.tag)
      .filter((tag) => !taken.has(tag))
      .slice(0, TAG_SUGGESTIONS);
  });

  protected problem(field: string): string | null {
    return this.showProblems() ? (this.problems().get(field) ?? null) : null;
  }

  protected hintOf(line: LineDraft): LineHint | null {
    const ingredient = line.ingredient;
    if (!ingredient) {
      return null;
    }
    if (ingredient.kind === 'new') {
      return { kind: 'new' };
    }
    const dislike = this.dislikeByIngredient().get(ingredient.id);
    if (!dislike) {
      return null;
    }
    return dislike.kind === 'never' ? { kind: 'never', reason: dislike.reason } : { kind: 'avoid' };
  }

  protected update(change: Partial<RecipeDraft>): void {
    this.edit((draft) => ({ ...draft, ...change }));
  }

  protected setNutrient(nutrient: Nutrient, value: string): void {
    this.edit((draft) => ({ ...draft, nutrition: { ...draft.nutrition, [nutrient]: value } }));
  }

  protected setLine(line: LineDraft, change: Partial<Omit<LineDraft, 'key'>>): void {
    this.edit((draft) => changeLine(draft, line.key, change));
  }

  protected pick(line: LineDraft, choice: IngredientChoice): void {
    this.edit((draft) => pickIngredient(draft, line.key, choice));
  }

  protected clearIngredient(line: LineDraft): void {
    this.edit((draft) => changeLine(draft, line.key, { ingredient: null }));
  }

  protected addLine(group: GroupDraft): void {
    this.edit((draft) => addLine(draft, group.key));
  }

  protected removeLine(line: LineDraft): void {
    this.edit((draft) => removeLine(draft, line.key));
  }

  protected moveLine(line: LineDraft, step: -1 | 1): void {
    this.edit((draft) => moveLine(draft, line.key, step));
  }

  protected moveLineTo(line: LineDraft, groupKey: string): void {
    this.edit((draft) => moveLineToGroup(draft, line.key, Number(groupKey)));
  }

  protected addSection(): void {
    this.edit(addSection);
  }

  protected renameSection(section: GroupDraft, name: string): void {
    this.edit((draft) => renameSection(draft, section.key, name));
  }

  protected moveSection(section: GroupDraft, step: -1 | 1): void {
    this.edit((draft) => moveSection(draft, section.key, step));
  }

  protected removeSection(section: GroupDraft): void {
    this.edit((draft) => removeSection(draft, section.key));
  }

  protected onTagKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      this.addTag(this.tagText());
    } else if (event.key === 'Backspace' && !this.tagText()) {
      const tags = this.draft()?.tags ?? [];
      if (tags.length) {
        this.removeTag(tags[tags.length - 1]);
      }
    }
  }

  protected addTag(text: string): void {
    const draft = this.draft();
    if (!draft) {
      return;
    }
    const result = addTag(draft, text);
    this.tagProblem.set(result.problem);
    if (!result.problem) {
      this.draft.set(result.draft);
      this.tagText.set('');
    }
  }

  protected removeTag(tag: string): void {
    this.edit((draft) => removeTag(draft, tag));
    this.tagProblem.set(null);
  }

  protected addStep(): void {
    this.edit(addStep);
  }

  protected setStep(stepKey: number, text: string): void {
    this.edit((draft) => changeStep(draft, stepKey, text));
  }

  protected removeStep(stepKey: number): void {
    this.edit((draft) => removeStep(draft, stepKey));
  }

  protected moveStep(stepKey: number, step: -1 | 1): void {
    this.edit((draft) => moveStep(draft, stepKey, step));
  }

  protected async save(): Promise<void> {
    const draft = this.draft();
    if (!draft || this.saving()) {
      return;
    }
    if (this.problems().size) {
      this.showProblems.set(true);
      return;
    }
    const id = this.id();
    this.saving.set(true);
    this.saveError.set(null);
    try {
      const change = changeOf(draft);
      const saved = id
        ? await this.api.updateRecipe(id, change)
        : await this.api.createRecipe(change);
      const photoSaved = await this.savePhoto(saved.id);
      this.toasts.show(
        this.transloco.translate(
          !photoSaved
            ? 'recipes.editor.toast.photoFailed'
            : id
              ? 'recipes.editor.toast.saved'
              : 'recipes.editor.toast.added',
        ),
      );
      this.photoChange.set(KEEP_PHOTO);
      this.recipe.set(saved);
      await this.leave(recipePath(saved.id), !id);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound) {
        this.recipe.reload();
      } else {
        this.saveError.set(
          error instanceof HttpErrorResponse && error.status === HttpStatusCode.BadRequest
            ? 'invalid'
            : 'failed',
        );
      }
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(this.backPath());
  }

  /** For the route's `canDeactivate`: true when there is nothing to lose or the person chose to discard it. */
  async canLeave(): Promise<boolean> {
    if (this.leaveWithoutAsking || !this.dirty()) {
      return true;
    }
    const label = this.transloco.translate('recipes.editor.discard.title');
    const name = this.isNew() ? null : (this.saved()?.name ?? null);
    return (await this.dialogs.open(this.discardDialog(), { label, data: name })) === true;
  }

  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty()) {
      event.preventDefault();
    }
  }

  protected setPhoto(change: PhotoChange): void {
    this.photoChange.set(change);
    this.saveError.set(null);
  }

  /** Uploads or removes the photo of the saved recipe; false when that failed, which leaves the recipe saved. */
  private async savePhoto(id: string): Promise<boolean> {
    const change = this.photoChange();
    try {
      if (change.kind === 'set') {
        await this.api.setPhoto(id, change.file);
      } else if (change.kind === 'remove') {
        await this.api.removePhoto(id);
      }
      return true;
    } catch {
      return false;
    }
  }

  private edit(change: (draft: RecipeDraft) => RecipeDraft): void {
    this.draft.update((draft) => (draft ? change(draft) : draft));
    this.saveError.set(null);
  }

  private async leave(path: string[], replaceUrl = false): Promise<void> {
    this.leaveWithoutAsking = true;
    try {
      await this.router.navigate(path, { replaceUrl });
    } finally {
      this.leaveWithoutAsking = false;
    }
  }
}

function decimalSeparatorOf(language: string): string {
  return (1.5).toLocaleString(language).charAt(1);
}
