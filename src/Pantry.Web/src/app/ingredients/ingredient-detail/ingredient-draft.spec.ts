import { Ingredient } from '../../catalog/catalog-api';
import { aliasProblem, changeOf, conversionUnits, draftOf, MAX_ALIASES, newDraft, parseAmount, sameDraft } from './ingredient-draft';

const onion: Ingredient = {
  id: 'o',
  name: 'Cebula',
  aliases: ['cebulka'],
  baseUnit: 'g',
  category: 'vegetablesAndFruit',
  isStaple: false,
  needsReview: true,
  conversions: [{ unit: 'piece', amount: 150 }],
};

describe('ingredient draft', () => {
  it('starts a new ingredient in other, in grams, from a cleaned name', () => {
    expect(newDraft('  red   onion ')).toEqual({
      name: 'red onion',
      aliases: [],
      category: 'other',
      baseUnit: 'g',
      isStaple: false,
      conversions: [],
    });
  });

  it('compares drafts field by field', () => {
    const draft = draftOf(onion);

    expect(sameDraft(draft, draftOf(onion))).toBe(true);
    expect(sameDraft(draft, { ...draft, aliases: ['cebulka', 'szalotka'] })).toBe(false);
    expect(sameDraft(draft, { ...draft, conversions: [{ unit: 'piece', amount: 120 }] })).toBe(false);
  });

  it('sends cleaned names', () => {
    expect(changeOf({ ...draftOf(onion), name: ' Cebula  biała ', aliases: [' cebulka '] })).toMatchObject({
      name: 'Cebula biała',
      aliases: ['cebulka'],
    });
  });

  it('refuses an alias that repeats a name, ignoring case and diacritics, and one too many', () => {
    const draft = draftOf(onion);

    expect(aliasProblem(draft, ' ')).toBe('empty');
    expect(aliasProblem(draft, 'CEBULA')).toBe('repeated');
    expect(aliasProblem(draft, 'Cebulka')).toBe('repeated');
    expect(aliasProblem({ ...draft, name: 'Żółta cebula' }, 'zolta CEBULA')).toBe('repeated');
    expect(aliasProblem(draft, 'szalotka')).toBeNull();
    expect(aliasProblem({ ...draft, aliases: Array.from({ length: MAX_ALIASES }, (_, i) => `a${i}`) }, 'szalotka')).toBe('tooMany');
  });

  it('offers conversions only for units that do not convert on their own and have none yet', () => {
    const grams = conversionUnits(draftOf(onion));
    const millilitres = conversionUnits({ ...draftOf(onion), baseUnit: 'ml', conversions: [] });

    expect(grams).not.toContain('g');
    expect(grams).not.toContain('kg');
    expect(grams).not.toContain('piece');
    expect(grams).toContain('ml');
    expect(millilitres).not.toEqual(expect.arrayContaining(['ml', 'l', 'tbsp', 'tsp']));
    expect(millilitres).toContain('piece');
  });

  it('reads amounts with a point or a comma', () => {
    expect(parseAmount('40,5')).toBe(40.5);
    expect(parseAmount(' 1.25 ')).toBe(1.25);
    expect(parseAmount('')).toBe('notPositive');
    expect(parseAmount('0')).toBe('notPositive');
    expect(parseAmount('-3')).toBe('notPositive');
    expect(parseAmount('abc')).toBe('invalid');
    expect(parseAmount('1.23456')).toBe('invalid');
    expect(parseAmount('1000001')).toBe('invalid');
  });
});
