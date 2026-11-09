import { Ingredient } from '../../catalog/catalog-api';
import { Recipe } from '../recipes-api';
import {
  addLine,
  addSection,
  addStep,
  addTag,
  changeLine,
  changeOf,
  changeStep,
  draftOf,
  moveLine,
  moveLineToGroup,
  moveSection,
  moveStep,
  newDraft,
  parseQuantity,
  pickIngredient,
  problemsOf,
  RecipeDraft,
  removeSection,
  removeTag,
  renameSection,
  sameDraft,
} from './recipe-draft';

const onion: Ingredient = {
  id: 'onion',
  name: 'onion',
  aliases: [],
  baseUnit: 'piece',
  category: 'vegetablesAndFruit',
  isStaple: false,
  needsReview: false,
  conversions: [],
};

const saved: Recipe = {
  id: 'r1',
  name: 'Pancakes',
  servings: 4,
  prepTimeMinutes: 25,
  sourceUrl: 'https://example.com/pancakes',
  tags: ['breakfast'],
  steps: ['Mix.', 'Fry.'],
  nutrition: { kcal: 520, protein: 24.5, fat: null, carbs: null },
  makeAgain: true,
  averageRating: null,
  ratingCount: 0,
  isFavourite: false,
  cooldownDays: null,
  cooldown: { days: 21, isHouseholdDefault: true, lastEaten: null, availableFrom: null },
  isArchived: false,
  archivedAt: null,
  createdAt: '2026-11-01T10:00:00Z',
  updatedAt: '2026-11-01T10:00:00Z',
  lines: [
    {
      ingredientId: 'salt',
      ingredientName: 'salt',
      ingredientNeedsReview: false,
      quantity: null,
      quantityPerServing: null,
      unit: null,
      note: null,
    },
  ],
  sections: [
    {
      name: 'Batter',
      lines: [
        {
          ingredientId: 'flour',
          ingredientName: 'flour',
          ingredientNeedsReview: false,
          quantity: 250,
          quantityPerServing: 62.5,
          unit: 'g',
          note: 'sifted',
        },
        {
          ingredientId: 'milk',
          ingredientName: 'milk',
          ingredientNeedsReview: false,
          quantity: 0.5,
          quantityPerServing: 0.125,
          unit: 'l',
          note: null,
        },
      ],
    },
  ],
  dislikes: [],
};

const keyOf = (draft: RecipeDraft, group: number, line: number) =>
  draft.groups[group].lines[line].key;

describe('recipe draft', () => {
  it('turns a saved recipe into a draft and back into the same change', () => {
    const draft = draftOf(saved, ',');

    expect(draft.groups[1].lines[1].quantity).toBe('0,5');
    expect(draft.nutrition.protein).toBe('24,5');
    expect(draft.groups[0].lines[0].quantity).toBe('');
    expect(changeOf(draft)).toEqual({
      name: 'Pancakes',
      servings: 4,
      prepTimeMinutes: 25,
      sourceUrl: 'https://example.com/pancakes',
      tags: ['breakfast'],
      steps: ['Mix.', 'Fry.'],
      nutrition: { kcal: 520, protein: 24.5, fat: null, carbs: null },
      makeAgain: true,
      cooldownDays: null,
      lines: [{ ingredientId: 'salt', quantity: null, unit: null, note: null }],
      sections: [
        {
          name: 'Batter',
          lines: [
            { ingredientId: 'flour', quantity: 250, unit: 'g', note: 'sifted' },
            { ingredientId: 'milk', quantity: 0.5, unit: 'l', note: null },
          ],
        },
      ],
    });
  });

  it('starts a new recipe for 4 servings with one empty line and step, and nothing to save but a name', () => {
    const draft = newDraft();

    expect(draft.servings).toBe('4');
    expect(draft.makeAgain).toBe(true);
    expect([...problemsOf(draft).values()]).toEqual(['nameRequired']);
    expect(changeOf({ ...draft, name: ' Soup ' })).toMatchObject({
      name: 'Soup',
      lines: [],
      sections: [],
      steps: [],
    });
  });

  it('sends a new ingredient by name and leaves the unit out of a line to taste', () => {
    let draft = newDraft();
    const line = keyOf(draft, 0, 0);
    draft = pickIngredient(draft, line, { kind: 'new', name: '  smoked   paprika ' });
    draft = changeLine(draft, line, { unit: 'tsp' });
    draft = addLine(draft, draft.groups[0].key);
    draft = pickIngredient(draft, keyOf(draft, 0, 1), { kind: 'existing', ingredient: onion });
    draft = changeLine(draft, keyOf(draft, 0, 1), { quantity: '1,5', note: ' chopped ' });

    expect(changeOf({ ...draft, name: 'Soup' }).lines).toEqual([
      { ingredientName: 'smoked paprika', quantity: null, unit: null, note: null },
      { ingredientId: 'onion', quantity: 1.5, unit: 'piece', note: 'chopped' },
    ]);
  });

  it('takes the base unit of a picked ingredient only while the line has no quantity', () => {
    let draft = newDraft();
    const line = keyOf(draft, 0, 0);
    draft = pickIngredient(draft, line, { kind: 'existing', ingredient: onion });
    expect(draft.groups[0].lines[0].unit).toBe('piece');

    draft = changeLine(draft, line, { quantity: '200', unit: 'g', ingredient: null });
    draft = pickIngredient(draft, line, { kind: 'existing', ingredient: onion });
    expect(draft.groups[0].lines[0].unit).toBe('g');
  });

  it.each([
    ['', null],
    ['2', 2],
    ['0,25', 0.25],
    ['.5', 0.5],
    ['1.125', 1.125],
    ['0', 'invalid'],
    ['-1', 'invalid'],
    ['1.0005', 'invalid'],
    ['abc', 'invalid'],
    ['100001', 'invalid'],
  ])('reads the quantity %j as %j', (text, expected) => {
    expect(parseQuantity(text)).toBe(expected);
  });

  it('reports what keeps the recipe from being saved', () => {
    let draft: RecipeDraft = {
      ...newDraft(),
      name: 'Soup',
      servings: '0',
      prepTime: 'soon',
      cooldownDays: '400',
      sourceUrl: 'ftp://x',
    };
    draft = changeLine(draft, keyOf(draft, 0, 0), { quantity: '2' });
    draft = { ...draft, nutrition: { ...draft.nutrition, fat: 'a lot' } };
    draft = addSection(draft);
    draft = renameSection(draft, draft.groups[1].key, 'Sauce');

    const problems = problemsOf(draft);

    expect(Object.fromEntries(problems)).toEqual({
      servings: 'servings',
      prepTime: 'prepTime',
      cooldownDays: 'cooldown',
      sourceUrl: 'sourceUrl',
      'nutrition.fat': 'nutrition',
      [`line.${keyOf(draft, 0, 0)}.ingredient`]: 'ingredientRequired',
      [`section.${draft.groups[1].key}`]: 'sectionEmpty',
    });
  });

  it('wants section names, different ones, and drops a section left unnamed and empty', () => {
    let draft = { ...draftOf(saved), name: 'Pancakes' };
    draft = addSection(draft);
    draft = addSection(draft);
    draft = addSection(draft);
    const [, batter, twin, unnamed, empty] = draft.groups;
    draft = renameSection(draft, twin.key, ' batter ');
    draft = pickIngredient(draft, twin.lines[0].key, { kind: 'existing', ingredient: onion });
    draft = pickIngredient(draft, unnamed.lines[0].key, { kind: 'existing', ingredient: onion });

    const problems = problemsOf(draft);

    expect(problems.get(`section.${batter.key}`)).toBeUndefined();
    expect(problems.get(`section.${twin.key}`)).toBe('sectionNameTaken');
    expect(problems.get(`section.${unnamed.key}`)).toBe('sectionName');
    expect(problems.get(`section.${empty.key}`)).toBeUndefined();
  });

  it('reorders lines within a group and moves them between sections', () => {
    let draft = draftOf(saved);
    const [loose, batter] = draft.groups;
    const flour = batter.lines[0].key;

    draft = moveLine(draft, flour, 1);
    expect(draft.groups[1].lines.map((l) => l.note)).toEqual(['', 'sifted']);
    draft = moveLine(draft, flour, 1);
    expect(draft.groups[1].lines.map((l) => l.note)).toEqual(['', 'sifted']);

    draft = moveLineToGroup(draft, flour, loose.key);
    expect(changeOf(draft).lines.map((l) => l.ingredientId)).toEqual(['salt', 'flour']);
    expect(changeOf(draft).sections[0].lines.map((l) => l.ingredientId)).toEqual(['milk']);
  });

  it('reorders sections after the lines outside any section and keeps the lines of a removed one', () => {
    let draft = addSection(draftOf(saved));
    const [, batter, topping] = draft.groups;
    draft = renameSection(draft, topping.key, 'Topping');
    draft = pickIngredient(draft, topping.lines[0].key, { kind: 'existing', ingredient: onion });

    draft = moveSection(draft, topping.key, -1);
    expect(draft.groups.map((g) => g.name)).toEqual([null, 'Topping', 'Batter']);
    draft = moveSection(draft, topping.key, -1);
    expect(draft.groups.map((g) => g.name)).toEqual([null, 'Topping', 'Batter']);

    draft = removeSection(draft, batter.key);
    expect(draft.groups.map((g) => g.name)).toEqual([null, 'Topping']);
    expect(changeOf(draft).lines.map((l) => l.ingredientId)).toEqual(['salt', 'flour', 'milk']);
  });

  it('normalizes tags, ignores repeats and stops at the limits', () => {
    let draft = newDraft();
    draft = addTag(draft, '  Quick   Dinner ').draft;
    expect(addTag(draft, 'quick dinner').draft.tags).toEqual(['quick dinner']);
    expect(addTag(draft, 'x'.repeat(41)).problem).toBe('tooLong');

    const full = { ...draft, tags: Array.from({ length: 20 }, (_, i) => `tag ${i}`) };
    expect(addTag(full, 'one more').problem).toBe('tooMany');
    expect(removeTag(draft, 'quick dinner').tags).toEqual([]);
  });

  it('edits, reorders and leaves out empty steps', () => {
    let draft = { ...newDraft(), name: 'Soup' };
    draft = changeStep(draft, draft.steps[0].key, 'Chop.');
    draft = addStep(draft);
    draft = changeStep(draft, draft.steps[1].key, 'Boil.');
    draft = addStep(draft);
    draft = moveStep(draft, draft.steps[1].key, -1);

    expect(changeOf(draft).steps).toEqual(['Boil.', 'Chop.']);
  });

  it('counts a draft as unchanged when only blank lines and steps were added', () => {
    const draft = draftOf(saved);

    expect(sameDraft(draft, addStep(addLine(draft, draft.groups[0].key)))).toBe(true);
    expect(sameDraft(draft, changeLine(draft, keyOf(draft, 1, 0), { quantity: '300' }))).toBe(
      false,
    );
    expect(sameDraft(draft, { ...draft, makeAgain: false })).toBe(false);
  });
});
