import { cleanIngredientName, findInName, ingredientNameKey } from './ingredient-name';

describe('ingredient names', () => {
  it.each([
    ['Żółty ser', 'zolty ser'],
    ['  Ser   ŻÓŁTY ', 'ser zolty'],
    ['Łosoś', 'losos'],
    ['Weißwurst', 'weisswurst'],
    ['Crème brûlée', 'creme brulee'],
  ])('compares %s as %s, like the API', (name, key) => {
    expect(ingredientNameKey(name)).toBe(key);
  });

  it('cleans names the way they are stored', () => {
    expect(cleanIngredientName('  cebula \t dymka ')).toBe('cebula dymka');
  });

  it('finds the matched part in the original text', () => {
    expect(findInName('Ser żółty', 'ZOLT')).toEqual({ start: 4, end: 8 });
    expect(findInName('Weißwurst', 'issw')).toEqual({ start: 2, end: 5 });
    expect(findInName('Cebula', 'ser')).toBeNull();
    expect(findInName('Cebula', '  ')).toBeNull();
  });
});
