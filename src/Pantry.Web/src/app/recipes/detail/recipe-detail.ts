import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { Quantity } from '../../catalog/quantity/quantity-formatter';
import { QuantityPipe } from '../../catalog/quantity/quantity.pipe';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { Toasts } from '../../core/toast/toasts';
import { DislikeKind, MEAL_SLOTS_URL, MealSlot, MEMBERS_URL, MembersResponse } from '../../household/household-api';
import { Icon } from '../../shared/icon/icon';
import { Switch } from '../../shared/switch/switch';
import {
  HistoryEntry,
  Nutrition,
  Recipe,
  RecipeDislike,
  RecipeLine,
  recipeHistoryUrl,
  recipeUrl,
  RecipesApi,
} from '../recipes-api';
import { editRecipePath, RECIPES_PATH } from '../recipes-paths';
import { Stars } from '../stars';
import { scaledAmount, startingServings, stepServings } from './recipe-servings';

export const PLANNER_PATH = '/planner';
export const RECIPE_DATE_FORMAT = 'd MMM y';

const MINUTES_PER_HOUR = 60;
const NUTRIENTS =['kcal', 'protein', 'fat', 'carbs'] as const satisfies readonly (keyof Nutrition)[];

/** An ingredient line as the detail shows it: scaled to the chosen servings, null quantity for *to taste*. */
export interface LineView {
  readonly line: RecipeLine;
  readonly quantity: Quantity | null;
  readonly dislike: DislikeKind | null;
}

export interface GroupView {
  readonly name: string | null;
  readonly lines: readonly LineView[];
}

type Busy = 'archive' | 'makeAgain';

/**
 * A recipe ready to cook (`/recipes/:id`): facts, dislike warnings, ingredients scaled to the household serving with
 * a stepper for another count, steps, nutrition per serving, history with ratings and make again, and the actions
 * edit, add to plan, archive and restore.
 */
@Component({
  selector: 'app-recipe-detail',
  imports: [TranslocoDirective, RouterLink, HlmButton, Icon, Switch, Stars, QuantityPipe, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './recipe-detail.html',
})
export class RecipeDetail {
  /** The route's `:id`. */
  readonly id = input.required<string>();

  private readonly api = inject(RecipesApi);
  private readonly toasts = inject(Toasts);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(UiLanguageService).current;

  protected readonly recipesPath = RECIPES_PATH;
  protected readonly plannerPath = PLANNER_PATH;
  protected readonly dateFormat = RECIPE_DATE_FORMAT;
  protected readonly nutrients = NUTRIENTS;

  protected readonly recipe = httpResource<Recipe>(() => recipeUrl(this.id()));
  protected readonly history = httpResource<HistoryEntry[]>(() => recipeHistoryUrl(this.id()));
  private readonly members = httpResource<MembersResponse>(() => MEMBERS_URL);
  private readonly mealSlots = httpResource<MealSlot[]>(() => MEAL_SLOTS_URL);

  protected readonly current = computed(() => (this.recipe.hasValue() ? this.recipe.value() : null));
  protected readonly gone = computed(() => {
    const error = this.recipe.error();
    return error instanceof HttpErrorResponse && error.status === HttpStatusCode.NotFound;
  });
  protected readonly failed = computed(() => !!this.recipe.error() && !this.gone());
  protected readonly editPath = computed(() => editRecipePath(this.id()));

  protected readonly householdServings = computed(() =>
    startingServings(this.members.value()?.householdServing ?? 0, this.current()?.servings ?? 1),
  );
  private readonly chosenServings = linkedSignal<string, number | null>({
    source: this.id,
    computation: () => null,
  });
  protected readonly servings = computed(() => this.chosenServings() ?? this.householdServings());
  protected readonly servingsChanged = computed(() => this.servings() !== this.householdServings());

  protected readonly groups = computed<GroupView[]>(() => {
    const recipe = this.current();
    if (!recipe) {
      return [];
    }
    const dislikes = new Map(recipe.dislikes.map((d) => [d.ingredientId, d.kind]));
    const servings = this.servings();
    const view = (line: RecipeLine): LineView => ({
      line,
      quantity:
        line.quantityPerServing !== null && line.unit !== null
          ? { amount: scaledAmount(line.quantityPerServing, line.unit, servings), unit: line.unit }
          : null,
      dislike: dislikes.get(line.ingredientId) ?? null,
    });
    const groups: GroupView[] = recipe.lines.length ? [{ name: null, lines: recipe.lines.map(view) }] : [];
    return [...groups, ...recipe.sections.map((s) => ({ name: s.name, lines: s.lines.map(view) }))];
  });

  protected readonly never = computed(() => this.current()?.dislikes.filter((d) => d.kind === 'never') ?? []);
  protected readonly avoid = computed(() => this.current()?.dislikes.filter((d) => d.kind === 'avoid') ?? []);
  protected readonly neverReason = computed(() => this.never().find((d) => d.reason)?.reason ?? null);
  protected readonly tooSoon = computed(() => {
    const from = this.current()?.cooldown.availableFrom;
    return from && from > todayIso() ? from : null;
  });
  protected readonly hasNutrition = computed(() => {
    const nutrition = this.current()?.nutrition;
    return !!nutrition && NUTRIENTS.some((n) => nutrition[n] !== null);
  });
  private readonly slotNames = computed(
    () => new Map((this.mealSlots.value() ?? []).map((slot) => [slot.id, slot.name])),
  );

  protected readonly busy = signal<Busy | null>(null);
  protected readonly actionFailed = linkedSignal<string, boolean>({ source: this.id, computation: () => false });

  protected readonly prepTime = computed(() => prepTimeOf(this.current()?.prepTimeMinutes ?? null));

  protected names(dislikes: readonly RecipeDislike[]): string {
    const unknown = this.transloco.translate('recipes.detail.unknownIngredient');
    return dislikes.map((d) => d.ingredientName ?? unknown).join(', ');
  }

  protected slotName(entry: HistoryEntry): string | null {
    return this.slotNames().get(entry.mealSlotId) ?? null;
  }

  protected step(direction: 1 | -1): void {
    this.chosenServings.set(stepServings(this.servings(), direction));
  }

  protected resetServings(): void {
    this.chosenServings.set(null);
  }

  protected async setMakeAgain(makeAgain: boolean): Promise<void> {
    const recipe = this.current();
    if (!recipe) {
      return;
    }
    await this.run('makeAgain', async () => {
      const summary = await this.api.setMakeAgain(recipe.id, makeAgain);
      this.recipe.set({
        ...recipe,
        makeAgain: summary.makeAgain,
        averageRating: summary.averageRating,
        ratingCount: summary.ratingCount,
        isFavourite: summary.isFavourite,
      });
    });
  }

  protected async archive(): Promise<void> {
    await this.changeArchived(true);
  }

  protected async restore(): Promise<void> {
    await this.changeArchived(false);
  }

  private async changeArchived(archived: boolean): Promise<void> {
    const recipe = this.current();
    if (!recipe) {
      return;
    }
    await this.run('archive', async () => {
      const changed = archived ? await this.api.archiveRecipe(recipe.id) : await this.api.restoreRecipe(recipe.id);
      this.recipe.set(changed);
      this.toasts.show(this.transloco.translate(archived ? 'recipes.detail.toast.archived' : 'recipes.detail.toast.restored'));
    });
  }

  private async run(busy: Busy, action: () => Promise<void>): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(busy);
    this.actionFailed.set(false);
    try {
      await action();
    } catch {
      this.actionFailed.set(true);
    } finally {
      this.busy.set(null);
    }
  }
}

/** How long a recipe takes, split for the `recipes.detail.time.*` labels. */
export function prepTimeOf(
  minutes: number | null,
): { key: 'minutes' | 'hours' | 'hoursMinutes'; hours: number; minutes: number } | null {
  if (minutes === null) {
    return null;
  }
  const hours = Math.floor(minutes / MINUTES_PER_HOUR);
  const rest = minutes % MINUTES_PER_HOUR;
  const key = !hours ? 'minutes' : rest ? 'hoursMinutes' : 'hours';
  return { key, hours, minutes: hours ? rest : minutes };
}

function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}
