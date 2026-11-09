import { formatNumber } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, ElementRef, inject, input, linkedSignal, signal, TemplateRef, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import {
  CatalogApi,
  Ingredient,
  INGREDIENT_CATEGORIES,
  INGREDIENT_NAME_MAX_LENGTH,
  IngredientReferences,
  ingredientReferencesUrl,
  ingredientUrl,
  ModuleReferenceCount,
  NameConflict,
  nameConflictsOf,
  referencesOf,
  UnitConversion,
} from '../../catalog/catalog-api';
import { ingredientNameKey } from '../../catalog/ingredient-name';
import { BASE_UNITS, BaseUnit, GENERIC_CONVERSIONS, Unit } from '../../catalog/units';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { Toasts } from '../../core/toast/toasts';
import { DialogContext, DialogFrame, Dialogs } from '../../shared/dialog/dialogs';
import { Icon } from '../../shared/icon/icon';
import { Switch } from '../../shared/switch/switch';
import { IngredientsScreen } from '../ingredients-screen';
import { ingredientPath, INGREDIENTS_PATH } from '../ingredients-paths';
import {
  aliasProblem,
  AliasProblem,
  changeOf,
  conversionUnits,
  draftOf,
  hasName,
  IngredientDraft,
  MAX_ALIASES,
  newDraft,
  parseAmount,
  sameDraft,
} from './ingredient-draft';

/** Modules with their own labels; references of any other module show the module code. */
const KNOWN_MODULES = ['recipes', 'shopping', 'household'];
const AMOUNT_FORMAT = '1.0-4';

type SaveError = 'failed' | 'invalid' | 'deleteFailed';
type ConversionError = 'pickUnit' | 'notPositive' | 'invalid';

interface BaseUnitChange {
  unit: BaseUnit;
  base: BaseUnit;
  list: string;
}

/**
 * An ingredient's detail, which is its edit form (PAN-29): name, aliases, category, base unit, staple flag and
 * conversions, with *Confirm* for one that needs review, where it is used, and delete. Also the form for a new
 * ingredient (`/ingredients/new?name=`). Leaving with unsaved changes asks first (`confirmDiscardChanges`).
 */
@Component({
  selector: 'app-ingredient-detail',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon, Switch, DialogFrame],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5', '(window:beforeunload)': 'onBeforeUnload($event)' },
  templateUrl: './ingredient-detail.html',
})
export class IngredientDetail {
  /** The route's `:id`; absent for a new ingredient. */
  readonly id = input<string>();
  /** The `name` query parameter: what a new ingredient is called at first. */
  readonly name = input<string>();

  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(Toasts);
  private readonly dialogs = inject(Dialogs);
  private readonly transloco = inject(TranslocoService);
  protected readonly screen = inject(IngredientsScreen);
  protected readonly language = inject(UiLanguageService).current;

  protected readonly categories = INGREDIENT_CATEGORIES;
  protected readonly baseUnits = BASE_UNITS;
  protected readonly nameMaxLength = INGREDIENT_NAME_MAX_LENGTH;
  protected readonly maxAliases = MAX_ALIASES;
  protected readonly knownModules = KNOWN_MODULES;

  private readonly baseUnitDialog = viewChild.required<TemplateRef<DialogContext<BaseUnitChange, boolean>>>('baseUnitDialog');
  private readonly deleteDialog = viewChild.required<TemplateRef<DialogContext<string, boolean>>>('deleteDialog');
  private readonly inUseDialog = viewChild.required<TemplateRef<DialogContext<string, void>>>('inUseDialog');
  private readonly discardDialog = viewChild.required<TemplateRef<DialogContext<string | null, boolean>>>('discardDialog');
  private readonly baseUnitGroup = viewChild<ElementRef<HTMLElement>>('baseUnitGroup');

  protected readonly isNew = computed(() => !this.id());
  protected readonly ingredient = httpResource<Ingredient>(() => (this.id() ? ingredientUrl(this.id()!) : undefined));
  protected readonly references = httpResource<IngredientReferences>(() =>
    this.id() ? ingredientReferencesUrl(this.id()!) : undefined,
  );
  protected readonly saved = computed(() => (this.ingredient.hasValue() ? this.ingredient.value() : null));
  protected readonly gone = computed(() => {
    const error = this.ingredient.error();
    return error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound;
  });

  private readonly initial = computed<IngredientDraft | null>(() => {
    if (this.isNew()) {
      return newDraft(this.name() ?? '');
    }
    const saved = this.saved();
    return saved ? draftOf(saved) : null;
  });

  protected readonly draft = linkedSignal(() => this.initial());
  protected readonly dirty = computed(() => {
    const draft = this.draft();
    const initial = this.initial();
    return !!draft && !!initial && !sameDraft(draft, initial);
  });
  protected readonly canSave = computed(() => {
    const draft = this.draft();
    return !!draft && hasName(draft) && !this.saving();
  });

  protected readonly saving = signal(false);
  protected readonly saveError = linkedSignal<string | undefined, SaveError | null>({ source: this.id, computation: () => null });
  private readonly conflicts = linkedSignal<string | undefined, NameConflict[]>({ source: this.id, computation: () => [] });
  private leaveWithoutAsking = false;

  protected readonly aliasText = linkedSignal<string | undefined, string>({ source: this.id, computation: () => '' });
  protected readonly aliasError = linkedSignal<string | undefined, AliasProblem | null>({ source: this.id, computation: () => null });
  protected readonly conversionUnit = linkedSignal<string | undefined, Unit | ''>({ source: this.id, computation: () => '' });
  protected readonly conversionAmount = linkedSignal<string | undefined, string>({ source: this.id, computation: () => '' });
  protected readonly conversionError = linkedSignal<string | undefined, ConversionError | null>({
    source: this.id,
    computation: () => null,
  });

  /** A 409 for the name, while the name is still the one that clashed. */
  protected readonly nameConflict = computed(() => {
    const key = ingredientNameKey(this.draft()?.name ?? '');
    return this.conflicts().find((c) => ingredientNameKey(c.name) === key) ?? null;
  });
  /** 409s for aliases that are still in the form. */
  protected readonly aliasConflicts = computed(() => {
    const keys = new Set((this.draft()?.aliases ?? []).map(ingredientNameKey));
    const name = ingredientNameKey(this.draft()?.name ?? '');
    return this.conflicts().filter((c) => ingredientNameKey(c.name) !== name && keys.has(ingredientNameKey(c.name)));
  });

  protected readonly availableConversionUnits = computed(() => {
    const draft = this.draft();
    return draft ? conversionUnits(draft) : [];
  });
  protected readonly conversionLines = computed(() => {
    const draft = this.draft();
    this.language();
    return draft ? draft.conversions.map((c) => ({ unit: c.unit, text: this.conversionText(c, draft.baseUnit) })) : [];
  });
  protected readonly builtIn = computed(() => {
    const draft = this.draft();
    const language = this.language();
    if (!draft) {
      return '';
    }
    return GENERIC_CONVERSIONS.filter((c) => c.baseUnit === draft.baseUnit)
      .map((c) => this.conversionText({ unit: c.unit, amount: c.factor }, draft.baseUnit, language))
      .join(', ');
  });
  protected readonly usedIn = computed<ModuleReferenceCount[] | null>(() =>
    this.references.hasValue() ? this.references.value().references : null,
  );
  protected readonly canConfirmAndNext = computed(() => {
    const id = this.id();
    return !!id && this.screen.needsReviewOnly() && this.screen.nextToReview(id) !== null;
  });

  protected update(change: Partial<IngredientDraft>): void {
    this.draft.update((draft) => (draft ? { ...draft, ...change } : draft));
    this.saveError.set(null);
  }

  protected onAliasKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      this.addAlias();
    } else if (event.key === 'Backspace' && !this.aliasText()) {
      const aliases = this.draft()?.aliases ?? [];
      if (aliases.length) {
        this.removeAlias(aliases.length - 1);
      }
    }
  }

  protected addAlias(): void {
    const draft = this.draft();
    const text = this.aliasText();
    if (!draft || !ingredientNameKey(text)) {
      this.aliasError.set(null);
      return;
    }
    const problem = aliasProblem(draft, text);
    this.aliasError.set(problem);
    if (!problem) {
      this.update({ aliases: [...draft.aliases, text.trim().replace(/\s+/g, ' ')] });
      this.aliasText.set('');
    }
  }

  protected removeAlias(index: number): void {
    const draft = this.draft();
    if (draft) {
      this.update({ aliases: draft.aliases.filter((_, i) => i !== index) });
      this.aliasError.set(null);
    }
  }

  protected async chooseBaseUnit(unit: BaseUnit): Promise<void> {
    const draft = this.draft();
    if (!draft || unit === draft.baseUnit) {
      return;
    }
    if (draft.conversions.length) {
      const change: BaseUnitChange = {
        unit,
        base: draft.baseUnit,
        list: draft.conversions.map((c) => this.conversionText(c, draft.baseUnit)).join(', '),
      };
      const label = this.transloco.translate('ingredients.dialog.baseUnit.title', { unit: this.unitLabel(unit) });
      if (!(await this.dialogs.open(this.baseUnitDialog(), { label, data: change }))) {
        return;
      }
    }
    this.update({ baseUnit: unit, conversions: [] });
    this.conversionUnit.set('');
    this.conversionError.set(null);
  }

  /** Arrow keys move through the base units, as in a radio group. */
  protected onBaseUnitKeydown(event: KeyboardEvent, unit: BaseUnit): void {
    const step = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
    if (!step) {
      return;
    }
    event.preventDefault();
    const index = (BASE_UNITS.indexOf(unit) + step + BASE_UNITS.length) % BASE_UNITS.length;
    this.baseUnitGroup()?.nativeElement.querySelectorAll<HTMLElement>('[role="radio"]')[index]?.focus();
    void this.chooseBaseUnit(BASE_UNITS[index]);
  }

  protected addConversion(): void {
    const draft = this.draft();
    const unit = this.conversionUnit();
    if (!draft) {
      return;
    }
    if (!unit) {
      this.conversionError.set('pickUnit');
      return;
    }
    const amount = parseAmount(this.conversionAmount());
    if (typeof amount !== 'number') {
      this.conversionError.set(amount);
      return;
    }
    this.update({ conversions: [...draft.conversions, { unit, amount }] });
    this.conversionUnit.set('');
    this.conversionAmount.set('');
    this.conversionError.set(null);
  }

  protected removeConversion(unit: Unit): void {
    const draft = this.draft();
    if (draft) {
      this.update({ conversions: draft.conversions.filter((c) => c.unit !== unit) });
    }
  }

  protected async onSave(): Promise<void> {
    const isNew = this.isNew();
    const result = await this.save();
    if (!result) {
      return;
    }
    this.toasts.show(this.transloco.translate(isNew ? 'ingredients.toast.added' : 'ingredients.toast.saved'));
    if (isNew) {
      await this.leave(ingredientPath(result.id), true);
    }
  }

  protected cancel(): void {
    if (this.isNew()) {
      void this.leave([INGREDIENTS_PATH]);
      return;
    }
    this.draft.set(this.initial());
    this.resetMessages();
  }

  /** Confirms the ingredient, saving the form first when it changed; `next` then opens the next one to review. */
  protected async confirm(next: boolean): Promise<void> {
    const id = this.id();
    if (!id || this.saving()) {
      return;
    }
    if (this.dirty() && !(await this.save())) {
      return;
    }
    const nextId = next ? this.screen.nextToReview(id) : null;
    this.saving.set(true);
    try {
      this.ingredient.set(await this.api.confirmIngredient(id));
      this.screen.changed();
      this.toasts.show(this.transloco.translate('ingredients.toast.confirmed'));
      if (nextId) {
        await this.router.navigate(ingredientPath(nextId));
      }
    } catch {
      this.saveError.set('failed');
    } finally {
      this.saving.set(false);
    }
  }

  protected async delete(): Promise<void> {
    const id = this.id();
    const name = this.saved()?.name;
    if (!id || !name) {
      return;
    }
    const used = this.usedIn();
    if (used?.length) {
      await this.showInUse(name);
      return;
    }
    const label = this.transloco.translate('ingredients.dialog.delete.title', { name });
    if (!(await this.dialogs.open(this.deleteDialog(), { label, data: name }))) {
      return;
    }
    try {
      await this.api.deleteIngredient(id);
      this.screen.changed();
      this.toasts.show(this.transloco.translate('ingredients.toast.deleted'));
      await this.leave([INGREDIENTS_PATH]);
    } catch (error) {
      const references = referencesOf(error);
      if (references) {
        this.references.set({ references });
        await this.showInUse(name);
      } else {
        this.saveError.set('deleteFailed');
      }
    }
  }

  /** For the route's `canDeactivate`: true when there is nothing to lose or the person chose to discard it. */
  async canLeave(): Promise<boolean> {
    if (this.leaveWithoutAsking || !this.dirty()) {
      return true;
    }
    const label = this.transloco.translate('ingredients.dialog.discard.title');
    const name = this.isNew() ? null : (this.saved()?.name ?? null);
    return (await this.dialogs.open(this.discardDialog(), { label, data: name })) === true;
  }

  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty()) {
      event.preventDefault();
    }
  }

  protected unitLabel(unit: Unit): string {
    return this.transloco.translate(`catalog.unit.${unit}`);
  }

  protected usageText(references: ModuleReferenceCount[]): string {
    return references
      .map((r) =>
        KNOWN_MODULES.includes(r.module)
          ? this.transloco.translate(`ingredients.detail.usage.count.${r.module}`, { count: r.count })
          : this.transloco.translate('ingredients.detail.usage.count.other', { module: r.module, count: r.count }),
      )
      .join(', ');
  }

  private async save(): Promise<Ingredient | null> {
    const draft = this.draft();
    if (!draft || !this.canSave()) {
      return null;
    }
    this.saving.set(true);
    this.resetMessages();
    try {
      const change = changeOf(draft);
      const id = this.id();
      const result = id ? await this.api.updateIngredient(id, change) : await this.api.createIngredient(change);
      if (id) {
        this.ingredient.set(result);
      }
      this.screen.changed();
      return result;
    } catch (error) {
      const conflicts = nameConflictsOf(error);
      if (conflicts.length) {
        this.conflicts.set(conflicts);
      } else if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound) {
        this.ingredient.reload();
        this.screen.changed();
      } else {
        this.saveError.set(error instanceof HttpErrorResponse && error.status === HttpStatusCode.BadRequest ? 'invalid' : 'failed');
      }
      return null;
    } finally {
      this.saving.set(false);
    }
  }

  private async showInUse(name: string): Promise<void> {
    const label = this.transloco.translate('ingredients.dialog.inUse.title', { name });
    await this.dialogs.open(this.inUseDialog(), { label, data: name });
  }

  private async leave(path: string[], replaceUrl = false): Promise<void> {
    this.leaveWithoutAsking = true;
    try {
      await this.router.navigate(path, { replaceUrl });
    } finally {
      this.leaveWithoutAsking = false;
    }
  }

  private resetMessages(): void {
    this.saveError.set(null);
    this.conflicts.set([]);
    this.aliasError.set(null);
    this.conversionError.set(null);
  }

  private conversionText(conversion: UnitConversion, baseUnit: Unit, language = this.language()): string {
    const unit = this.transloco.translate(`catalog.unitAmount.${conversion.unit}`, { count: 1 });
    const amount = formatNumber(conversion.amount, language, AMOUNT_FORMAT);
    return `1 ${unit} = ${amount} ${this.unitLabel(baseUnit)}`;
  }
}
