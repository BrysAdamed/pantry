import { roundForMeasuring, scaledAmount, startingServings, stepServings } from './recipe-servings';

describe('recipe servings', () => {
  describe('roundForMeasuring', () => {
    it.each([
      [151, 'g', 150],
      [23.4, 'ml', 25],
      [20, 'g', 20],
      [17.6, 'g', 18],
      [4.6, 'ml', 5],
      [0.34, 'g', 0.3],
      [0.02, 'g', 0.1],
    ] as const)('rounds %s %s to %s', (amount, unit, expected) => {
      expect(roundForMeasuring(amount, unit)).toBeCloseTo(expected, 6);
    });

    it.each([
      [1.15, 'piece', 1],
      [1.3, 'clove', 1.5],
      [2.76, 'tbsp', 3],
      [0.2, 'tsp', 0.5],
      [0.6, 'pinch', 0.5],
      [3.24, 'can', 3],
    ] as const)('rounds %s %s to halves: %s', (amount, unit, expected) => {
      expect(roundForMeasuring(amount, unit)).toBe(expected);
    });

    it('rounds kilograms and litres the way it rounds grams and millilitres', () => {
      expect(roundForMeasuring(0.8051, 'kg')).toBeCloseTo(0.805, 6);
      expect(roundForMeasuring(1.2372, 'l')).toBeCloseTo(1.235, 6);
      expect(roundForMeasuring(0.0123, 'kg')).toBeCloseTo(0.012, 6);
    });

    it('keeps nothing as nothing', () => {
      expect(roundForMeasuring(0, 'piece')).toBe(0);
    });
  });

  describe('scaledAmount', () => {
    it('scales a per-serving quantity to the servings before rounding', () => {
      expect(scaledAmount(150, 'g', 2.3)).toBe(345);
      expect(scaledAmount(0.5, 'piece', 2.3)).toBe(1);
      expect(scaledAmount(1, 'clove', 2.3)).toBe(2.5);
      expect(scaledAmount(70, 'g', 1)).toBe(70);
    });
  });

  describe('startingServings', () => {
    it('starts with the household serving', () => {
      expect(startingServings(2.3, 4)).toBe(2.3);
    });

    it('falls back to the recipe servings when nobody eats yet', () => {
      expect(startingServings(0, 4)).toBe(4);
    });
  });

  describe('stepServings', () => {
    it('steps to the next half serving', () => {
      expect(stepServings(2.3, 1)).toBe(2.5);
      expect(stepServings(2.3, -1)).toBe(2);
      expect(stepServings(2, 1)).toBe(2.5);
      expect(stepServings(2.5, -1)).toBe(2);
    });

    it('stays within half a serving and the recipe limit', () => {
      expect(stepServings(0.5, -1)).toBe(0.5);
      expect(stepServings(0.3, -1)).toBe(0.5);
      expect(stepServings(100, 1)).toBe(100);
    });
  });
});
