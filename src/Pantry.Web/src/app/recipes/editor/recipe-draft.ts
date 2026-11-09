import { IngredientChoice } from '../../catalog/catalog-api';
import { cleanIngredientName } from '../../catalog/ingredient-name';
import { Unit } from '../../catalog/units';
import {
  Nutrition,
  Recipe,
  RECIPE_LIMITS,
  RecipeChange,
  RecipeLine,
  RecipeLineChange,
} from '../recipes-api';

export const DEFAULT_SERVINGS = 4;
export const DEFAULT_UNIT: Unit = 'g';
export const NUTRIENTS = [
  'kcal',
  'protein',
  'fat',
  'carbs',
] as const satisfies readonly (keyof Nutrition)[];

export type Nutrient = (typeof NUTRIENTS)[number];

/** The ingredient of a line: a catalog ingredient, or a new name the API adds to the catalog when the recipe is saved. */
export type LineIngredient =
  | {
      readonly kind: 'existing';
      readonly id: string;
      readonly name: string | null;
      readonly needsReview: boolean;
    }
  | { readonly kind: 'new'; readonly name: string };

/** An ingredient line as typed: an empty quantity means *to taste*, and then the unit is not saved. */
export interface LineDraft {
  readonly key: number;
  readonly quantity: string;
  readonly unit: Unit;
  readonly ingredient: LineIngredient | null;
  readonly note: string;
}

/** A group of lines: the first one (`name` null) holds the lines outside any section, the others are sections in order. */
export interface GroupDraft {
  readonly key: number;
  readonly name: string | null;
  readonly lines: readonly LineDraft[];
}

export interface StepDraft {
  readonly key: number;
  readonly text: string;
}

/** The recipe form as a person edits it; quantities are for `servings`. The API gets it as a `RecipeChange`. */
export interface RecipeDraft {
  readonly name: string;
  readonly servings: string;
  readonly prepTime: string;
  readonly sourceUrl: string;
  readonly tags: readonly string[];
  readonly steps: readonly StepDraft[];
  readonly nutrition: Readonly<Record<Nutrient, string>>;
  readonly makeAgain: boolean;
  readonly cooldownDays: string;
  readonly groups: readonly GroupDraft[];
}

export type RecipeProblem =
  | 'nameRequired'
  | 'servings'
  | 'prepTime'
  | 'sourceUrl'
  | 'cooldown'
  | 'nutrition'
  | 'quantity'
  | 'ingredientRequired'
  | 'sectionName'
  | 'sectionNameTaken'
  | 'sectionEmpty'
  | 'tooManyLines'
  | 'tooManySteps';

/** What is wrong, by field id: `name`, `servings`, `nutrition.kcal`, `line.<key>.quantity`, `section.<key>`... */
export type RecipeProblems = ReadonlyMap<string, RecipeProblem>;

export type TagProblem = 'tooLong' | 'tooMany';

let lastKey = 0;
const nextKey = () => ++lastKey;

export function emptyLine(): LineDraft {
  return { key: nextKey(), quantity: '', unit: DEFAULT_UNIT, ingredient: null, note: '' };
}

export function emptyStep(): StepDraft {
  return { key: nextKey(), text: '' };
}

export function newDraft(): RecipeDraft {
  return {
    name: '',
    servings: String(DEFAULT_SERVINGS),
    prepTime: '',
    sourceUrl: '',
    tags: [],
    steps: [emptyStep()],
    nutrition: { kcal: '', protein: '', fat: '', carbs: '' },
    makeAgain: true,
    cooldownDays: '',
    groups: [{ key: nextKey(), name: null, lines: [emptyLine()] }],
  };
}

/** The draft of a saved recipe; numbers are written with `decimalSeparator`. */
export function draftOf(recipe: Recipe, decimalSeparator = '.'): RecipeDraft {
  const text = (value: number | null) =>
    value === null ? '' : String(value).replace('.', decimalSeparator);
  const line = (l: RecipeLine): LineDraft => ({
    key: nextKey(),
    quantity: text(l.quantity),
    unit: l.unit ?? DEFAULT_UNIT,
    ingredient: {
      kind: 'existing',
      id: l.ingredientId,
      name: l.ingredientName,
      needsReview: l.ingredientNeedsReview,
    },
    note: l.note ?? '',
  });
  return {
    name: recipe.name,
    servings: String(recipe.servings),
    prepTime: text(recipe.prepTimeMinutes),
    sourceUrl: recipe.sourceUrl ?? '',
    tags: recipe.tags,
    steps: recipe.steps.length
      ? recipe.steps.map((s) => ({ key: nextKey(), text: s }))
      : [emptyStep()],
    nutrition: {
      kcal: text(recipe.nutrition.kcal),
      protein: text(recipe.nutrition.protein),
      fat: text(recipe.nutrition.fat),
      carbs: text(recipe.nutrition.carbs),
    },
    makeAgain: recipe.makeAgain,
    cooldownDays: text(recipe.cooldownDays),
    groups: [
      { key: nextKey(), name: null, lines: recipe.lines.map(line) },
      ...recipe.sections.map((s) => ({ key: nextKey(), name: s.name, lines: s.lines.map(line) })),
    ],
  };
}

/** True when both drafts would save the same recipe; blank lines and steps do not count. */
export function sameDraft(a: RecipeDraft, b: RecipeDraft): boolean {
  return JSON.stringify(comparable(a)) === JSON.stringify(comparable(b));
}

export function isBlankLine(line: LineDraft): boolean {
  return !line.ingredient && !line.quantity.trim() && !line.note.trim();
}

export function lineCount(draft: RecipeDraft): number {
  return draft.groups.reduce(
    (count, group) => count + group.lines.filter((l) => !isBlankLine(l)).length,
    0,
  );
}

/** A quantity typed with a point or a comma: null when empty (*to taste*), `'invalid'` when it cannot be saved. */
export function parseQuantity(text: string): number | null | 'invalid' {
  return parseDecimal(text, {
    min: 0,
    minInclusive: false,
    max: RECIPE_LIMITS.maxQuantity,
    decimals: RECIPE_LIMITS.quantityDecimals,
  });
}

export function problemsOf(draft: RecipeDraft): RecipeProblems {
  const problems = new Map<string, RecipeProblem>();
  if (!draft.name.trim()) {
    problems.set('name', 'nameRequired');
  }
  if (typeof parseWhole(draft.servings, 1, RECIPE_LIMITS.maxServings) !== 'number') {
    problems.set('servings', 'servings');
  }
  if (parseWhole(draft.prepTime, 1, RECIPE_LIMITS.maxPrepTimeMinutes) === 'invalid') {
    problems.set('prepTime', 'prepTime');
  }
  if (parseWhole(draft.cooldownDays, 0, RECIPE_LIMITS.maxCooldownDays) === 'invalid') {
    problems.set('cooldownDays', 'cooldown');
  }
  if (draft.sourceUrl.trim() && !isWebAddress(draft.sourceUrl.trim())) {
    problems.set('sourceUrl', 'sourceUrl');
  }
  for (const nutrient of NUTRIENTS) {
    if (parseNutrient(draft.nutrition[nutrient]) === 'invalid') {
      problems.set(`nutrition.${nutrient}`, 'nutrition');
    }
  }
  for (const line of draft.groups.flatMap((g) => g.lines).filter((l) => !isBlankLine(l))) {
    if (parseQuantity(line.quantity) === 'invalid') {
      problems.set(`line.${line.key}.quantity`, 'quantity');
    }
    if (!line.ingredient) {
      problems.set(`line.${line.key}.ingredient`, 'ingredientRequired');
    }
  }
  const sectionNames = new Set<string>();
  for (const section of draft.groups.slice(1)) {
    const name = sectionKey(section.name ?? '');
    const hasLines = section.lines.some((l) => !isBlankLine(l));
    if (!name && hasLines) {
      problems.set(`section.${section.key}`, 'sectionName');
    } else if (name && !hasLines) {
      problems.set(`section.${section.key}`, 'sectionEmpty');
    } else if (name && sectionNames.has(name)) {
      problems.set(`section.${section.key}`, 'sectionNameTaken');
    }
    sectionNames.add(name);
  }
  if (lineCount(draft) > RECIPE_LIMITS.maxLines) {
    problems.set('lines', 'tooManyLines');
  }
  if (draft.steps.filter((s) => s.text.trim()).length > RECIPE_LIMITS.maxSteps) {
    problems.set('steps', 'tooManySteps');
  }
  return problems;
}

/** The recipe to save; call it only for a draft without problems. Blank lines, sections and steps are left out. */
export function changeOf(draft: RecipeDraft): RecipeChange {
  const [loose, ...sections] = draft.groups;
  const nutrition = Object.fromEntries(
    NUTRIENTS.map((n) => [n, numberOrNull(parseNutrient(draft.nutrition[n]))]),
  ) as unknown as Nutrition;
  return {
    name: draft.name.trim(),
    servings: Number(draft.servings.trim()),
    prepTimeMinutes: numberOrNull(parseWhole(draft.prepTime, 1, RECIPE_LIMITS.maxPrepTimeMinutes)),
    sourceUrl: draft.sourceUrl.trim() || null,
    tags: [...draft.tags],
    steps: draft.steps.map((s) => s.text.trim()).filter(Boolean),
    nutrition,
    makeAgain: draft.makeAgain,
    cooldownDays: numberOrNull(parseWhole(draft.cooldownDays, 0, RECIPE_LIMITS.maxCooldownDays)),
    lines: linesToSave(loose.lines),
    sections: sections
      .filter((s) => s.lines.some((l) => !isBlankLine(l)))
      .map((s) => ({ name: (s.name ?? '').trim(), lines: linesToSave(s.lines) })),
  };
}

/** The ingredient a line gets from the picker. Without a quantity yet, the unit follows the ingredient's base unit. */
export function pickIngredient(
  draft: RecipeDraft,
  lineKey: number,
  choice: IngredientChoice,
): RecipeDraft {
  return updateLine(draft, lineKey, (line) =>
    choice.kind === 'existing'
      ? {
          ...line,
          ingredient: {
            kind: 'existing',
            id: choice.ingredient.id,
            name: choice.ingredient.name,
            needsReview: choice.ingredient.needsReview,
          },
          unit: line.quantity.trim() ? line.unit : choice.ingredient.baseUnit,
        }
      : { ...line, ingredient: { kind: 'new', name: cleanIngredientName(choice.name) } },
  );
}

export function changeLine(
  draft: RecipeDraft,
  lineKey: number,
  change: Partial<Omit<LineDraft, 'key'>>,
): RecipeDraft {
  return updateLine(draft, lineKey, (line) => ({ ...line, ...change }));
}

export function addLine(draft: RecipeDraft, groupKey: number): RecipeDraft {
  return updateGroup(draft, groupKey, (group) => ({
    ...group,
    lines: [...group.lines, emptyLine()],
  }));
}

export function removeLine(draft: RecipeDraft, lineKey: number): RecipeDraft {
  return {
    ...draft,
    groups: draft.groups.map((g) => ({ ...g, lines: g.lines.filter((l) => l.key !== lineKey) })),
  };
}

/** Moves a line up (-1) or down (1) within its group. */
export function moveLine(draft: RecipeDraft, lineKey: number, step: -1 | 1): RecipeDraft {
  return {
    ...draft,
    groups: draft.groups.map((g) =>
      g.lines.some((l) => l.key === lineKey) ? { ...g, lines: moved(g.lines, lineKey, step) } : g,
    ),
  };
}

/** Moves a line to the end of another group (a section, or the lines outside any section). */
export function moveLineToGroup(
  draft: RecipeDraft,
  lineKey: number,
  groupKey: number,
): RecipeDraft {
  const line = draft.groups.flatMap((g) => g.lines).find((l) => l.key === lineKey);
  if (!line || !draft.groups.some((g) => g.key === groupKey)) {
    return draft;
  }
  return updateGroup(removeLine(draft, lineKey), groupKey, (group) => ({
    ...group,
    lines: [...group.lines, line],
  }));
}

export function addSection(draft: RecipeDraft): RecipeDraft {
  return {
    ...draft,
    groups: [...draft.groups, { key: nextKey(), name: '', lines: [emptyLine()] }],
  };
}

export function renameSection(draft: RecipeDraft, groupKey: number, name: string): RecipeDraft {
  return updateGroup(draft, groupKey, (group) =>
    group.name === null ? group : { ...group, name },
  );
}

/** Moves a section up (-1) or down (1) among the sections; the lines outside any section stay first. */
export function moveSection(draft: RecipeDraft, groupKey: number, step: -1 | 1): RecipeDraft {
  const [loose, ...sections] = draft.groups;
  return { ...draft, groups: [loose, ...moved(sections, groupKey, step)] };
}

/** Removes a section; its lines that are not blank move to the end of the lines outside any section. */
export function removeSection(draft: RecipeDraft, groupKey: number): RecipeDraft {
  const [loose, ...sections] = draft.groups;
  const section = sections.find((s) => s.key === groupKey);
  if (!section) {
    return draft;
  }
  const kept = section.lines.filter((l) => !isBlankLine(l));
  return {
    ...draft,
    groups: [
      { ...loose, lines: [...loose.lines, ...kept] },
      ...sections.filter((s) => s !== section),
    ],
  };
}

/** A tag the way the API stores it: trimmed, single spaces, lower case. */
export function normalizeTag(text: string): string {
  return text.trim().replace(/\s+/g, ' ').toLowerCase();
}

/** Adds a tag unless it is blank or already there; `problem` says why it could not be added. */
export function addTag(
  draft: RecipeDraft,
  text: string,
): { draft: RecipeDraft; problem: TagProblem | null } {
  const tag = normalizeTag(text);
  if (!tag || draft.tags.includes(tag)) {
    return { draft, problem: null };
  }
  if (tag.length > RECIPE_LIMITS.tagMaxLength) {
    return { draft, problem: 'tooLong' };
  }
  if (draft.tags.length >= RECIPE_LIMITS.maxTags) {
    return { draft, problem: 'tooMany' };
  }
  return { draft: { ...draft, tags: [...draft.tags, tag] }, problem: null };
}

export function removeTag(draft: RecipeDraft, tag: string): RecipeDraft {
  return { ...draft, tags: draft.tags.filter((t) => t !== tag) };
}

export function addStep(draft: RecipeDraft): RecipeDraft {
  return { ...draft, steps: [...draft.steps, emptyStep()] };
}

export function changeStep(draft: RecipeDraft, stepKey: number, text: string): RecipeDraft {
  return { ...draft, steps: draft.steps.map((s) => (s.key === stepKey ? { ...s, text } : s)) };
}

export function removeStep(draft: RecipeDraft, stepKey: number): RecipeDraft {
  return { ...draft, steps: draft.steps.filter((s) => s.key !== stepKey) };
}

export function moveStep(draft: RecipeDraft, stepKey: number, step: -1 | 1): RecipeDraft {
  return { ...draft, steps: moved(draft.steps, stepKey, step) };
}

function updateGroup(
  draft: RecipeDraft,
  groupKey: number,
  change: (group: GroupDraft) => GroupDraft,
): RecipeDraft {
  return { ...draft, groups: draft.groups.map((g) => (g.key === groupKey ? change(g) : g)) };
}

function updateLine(
  draft: RecipeDraft,
  lineKey: number,
  change: (line: LineDraft) => LineDraft,
): RecipeDraft {
  return {
    ...draft,
    groups: draft.groups.map((g) => ({
      ...g,
      lines: g.lines.map((l) => (l.key === lineKey ? change(l) : l)),
    })),
  };
}

function moved<T extends { key: number }>(items: readonly T[], key: number, step: -1 | 1): T[] {
  const from = items.findIndex((item) => item.key === key);
  const to = from + step;
  if (from < 0 || to < 0 || to >= items.length) {
    return [...items];
  }
  const result = [...items];
  [result[from], result[to]] = [result[to], result[from]];
  return result;
}

function linesToSave(lines: readonly LineDraft[]): RecipeLineChange[] {
  return lines
    .filter((l) => !isBlankLine(l) && l.ingredient)
    .map((l) => {
      const quantity = numberOrNull(parseQuantity(l.quantity));
      const ingredient = l.ingredient!;
      return {
        ...(ingredient.kind === 'existing'
          ? { ingredientId: ingredient.id }
          : { ingredientName: ingredient.name }),
        quantity,
        unit: quantity === null ? null : l.unit,
        note: l.note.trim() || null,
      };
    });
}

function comparable(draft: RecipeDraft): unknown {
  const line = (l: LineDraft) => ({
    quantity: l.quantity.trim(),
    unit: l.quantity.trim() ? l.unit : null,
    ingredient: l.ingredient?.kind === 'existing' ? l.ingredient.id : (l.ingredient?.name ?? null),
    note: l.note.trim(),
  });
  return {
    ...draft,
    name: draft.name.trim(),
    steps: draft.steps.map((s) => s.text.trim()).filter(Boolean),
    groups: draft.groups
      .map((g) => ({ name: g.name, lines: g.lines.filter((l) => !isBlankLine(l)).map(line) }))
      .filter((g, index) => index === 0 || g.lines.length || g.name),
  };
}

function sectionKey(name: string): string {
  return name.trim().replace(/\s+/g, ' ').toLowerCase();
}

function isWebAddress(text: string): boolean {
  if (text.length > RECIPE_LIMITS.sourceUrlMaxLength || !URL.canParse(text)) {
    return false;
  }
  const { protocol } = new URL(text);
  return protocol === 'http:' || protocol === 'https:';
}

function parseNutrient(text: string): number | null | 'invalid' {
  return parseDecimal(text, {
    min: 0,
    minInclusive: true,
    max: RECIPE_LIMITS.maxNutrition,
    decimals: null,
  });
}

function parseWhole(text: string, min: number, max: number): number | null | 'invalid' {
  const trimmed = text.trim();
  if (!trimmed) {
    return null;
  }
  const value = Number(trimmed);
  return /^\d+$/.test(trimmed) && value >= min && value <= max ? value : 'invalid';
}

function parseDecimal(
  text: string,
  rule: { min: number; minInclusive: boolean; max: number; decimals: number | null },
): number | null | 'invalid' {
  const normalized = text.trim().replace(/\s/g, '').replace(',', '.');
  if (!normalized) {
    return null;
  }
  if (!/^(\d+\.?\d*|\.\d+)$/.test(normalized)) {
    return 'invalid';
  }
  const value = Number(normalized);
  const decimals = normalized.includes('.') ? normalized.split('.')[1].length : 0;
  const aboveMin = rule.minInclusive ? value >= rule.min : value > rule.min;
  return aboveMin && value <= rule.max && (rule.decimals === null || decimals <= rule.decimals)
    ? value
    : 'invalid';
}

function numberOrNull(value: number | null | 'invalid'): number | null {
  return typeof value === 'number' ? value : null;
}
