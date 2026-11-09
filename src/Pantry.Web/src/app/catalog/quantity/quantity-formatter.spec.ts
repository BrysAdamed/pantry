import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { registerUiLocales } from '../../core/i18n/provide-i18n';
import { UiLanguage } from '../../core/i18n/ui-language';
import { provideTranslocoTesting } from '../../testing/transloco-testing';
import { Quantity, QuantityFormatter } from './quantity-formatter';
import { QuantityPipe } from './quantity.pipe';

const space = ' ';

describe('QuantityFormatter', () => {
  let formatter: QuantityFormatter;
  let transloco: TranslocoService;

  beforeAll(() => registerUiLocales());

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] });
    formatter = TestBed.inject(QuantityFormatter);
    transloco = TestBed.inject(TranslocoService);
  });

  function format(language: UiLanguage, quantity: Quantity | Quantity[]): string {
    transloco.setActiveLang(language);
    return formatter.format(quantity, language).replaceAll(space, ' ');
  }

  describe('in Polish', () => {
    it.each([
      [1, '1 ząbek'],
      [2, '2 ząbki'],
      [4, '4 ząbki'],
      [5, '5 ząbków'],
      [12, '12 ząbków'],
      [22, '22 ząbki'],
      [25, '25 ząbków'],
      [0, '0 ząbków'],
    ])('uses the plural form for %s', (amount, expected) => {
      expect(format('pl', { amount, unit: 'clove' })).toBe(expected);
    });

    it.each([
      [2.5, 'clove', '2,5 ząbka'],
      [0.5, 'tbsp', '0,5 łyżki'],
      [1.5, 'glass', '1,5 szklanki'],
      [0.25, 'tsp', '0,25 łyżeczki'],
    ] as const)('uses the genitive singular for the fraction %s %s', (amount, unit, expected) => {
      expect(format('pl', { amount, unit })).toBe(expected);
    });

    it('keeps abbreviations as they are', () => {
      expect(format('pl', { amount: 3, unit: 'piece' })).toBe('3 szt.');
      expect(format('pl', { amount: 200, unit: 'g' })).toBe('200 g');
    });

    it('writes a decimal comma and rounds to two decimals', () => {
      expect(format('pl', { amount: 2.333, unit: 'piece' })).toBe('2,33 szt.');
    });
  });

  describe('in English', () => {
    it.each([
      [1, 'clove', '1 clove'],
      [2, 'clove', '2 cloves'],
      [1.5, 'pinch', '1.5 pinches'],
      [3, 'tbsp', '3 tbsp'],
      [3, 'piece', '3 pc'],
    ] as const)('writes %s %s as %s', (amount, unit, expected) => {
      expect(format('en', { amount, unit })).toBe(expected);
    });

    it('writes a decimal point', () => {
      expect(format('en', { amount: 0.5, unit: 'l' })).toBe('0.5 l');
    });
  });

  describe('scaling', () => {
    it.each([
      ['pl', 1500, 'g', '1,5 kg'],
      ['en', 1500, 'g', '1.5 kg'],
      ['pl', 1000, 'g', '1 kg'],
      ['pl', 999, 'g', '999 g'],
      ['pl', 2250, 'ml', '2,25 l'],
      ['en', 12000, 'g', '12 kg'],
      ['pl', 1234, 'g', '1,23 kg'],
      ['pl', 999.999, 'g', '1 kg'],
    ] as const)('in %s writes %s %s as %s', (language, amount, unit, expected) => {
      expect(format(language, { amount, unit })).toBe(expected);
    });

    it('scales only grams and millilitres', () => {
      expect(format('pl', { amount: 1500, unit: 'piece' })).toBe('1 500 szt.');
      expect(format('en', { amount: 1500, unit: 'tsp' })).toBe('1,500 tsp');
    });
  });

  describe('multi-part amounts', () => {
    it('joins the parts with a plus', () => {
      const parts: Quantity[] = [
        { amount: 3, unit: 'piece' },
        { amount: 300, unit: 'g' },
      ];
      expect(format('pl', parts)).toBe('3 szt. + 300 g');
      expect(format('en', parts)).toBe('3 pc + 300 g');
    });

    it('formats every part on its own', () => {
      const parts: Quantity[] = [
        { amount: 2, unit: 'clove' },
        { amount: 1500, unit: 'ml' },
        { amount: 0.5, unit: 'bunch' },
      ];
      expect(format('pl', parts)).toBe('2 ząbki + 1,5 l + 0,5 pęczka');
    });

    it('writes nothing for no parts', () => {
      expect(format('pl', [])).toBe('');
    });
  });

  it('keeps the number and its unit together on one line', () => {
    transloco.setActiveLang('pl');
    expect(formatter.format({ amount: 200, unit: 'g' }, 'pl')).toBe(`200${space}g`);
  });
});

describe('QuantityPipe', () => {
  @Component({
    imports: [QuantityPipe],
    template: `<span>{{ quantity() | quantity: language() }}</span>`,
  })
  class Host {
    readonly quantity = signal<Quantity | Quantity[] | null>({ amount: 1500, unit: 'g' });
    readonly language = signal<UiLanguage>('pl');
  }

  beforeAll(() => registerUiLocales());

  beforeEach(() => TestBed.configureTestingModule({ providers: [provideTranslocoTesting()] }));

  it('re-renders when the language changes', async () => {
    const transloco = TestBed.inject(TranslocoService);
    transloco.setActiveLang('pl');
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent.replaceAll(space, ' ')).toBe('1,5 kg');

    transloco.setActiveLang('en');
    fixture.componentInstance.language.set('en');
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent.replaceAll(space, ' ')).toBe('1.5 kg');
  });

  it('writes nothing without a quantity', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.quantity.set(null);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toBe('');
  });
});
