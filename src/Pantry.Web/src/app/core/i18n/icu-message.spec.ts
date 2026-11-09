import { translations } from '../../testing/transloco-testing';
import { formatIcuMessage, isIcuMessage, parseIcuMessage } from './icu-message';
import { UI_LANGUAGES } from './ui-language';

function format(source: string, values: Record<string, unknown>, locale = 'en'): string {
  return formatIcuMessage(parseIcuMessage(source), values, locale);
}

function labelsOf(value: object): string[] {
  return Object.values(value).flatMap((child) => (typeof child === 'string' ? [child] : labelsOf(child)));
}

describe('ICU messages', () => {
  it('tells ICU labels from plain ones', () => {
    expect(isIcuMessage('{count, plural, one {x} other {y}}')).toBe(true);
    expect(isIcuMessage('{ level , select , never {x} other {y}}')).toBe(true);
    expect(isIcuMessage('Rename {{name}}')).toBe(false);
    expect(isIcuMessage('Nothing matches "{q}".')).toBe(false);
  });

  it('nests arguments, counts and selects inside cases', () => {
    const source =
      '{count, plural, =0 {Nothing} one {# {level, select, never {never} other {other}} item} other {# items}} of {total}';

    expect(format(source, { count: 0, total: 3 })).toBe('Nothing of 3');
    expect(format(source, { count: 1, level: 'never', total: 3 })).toBe('1 never item of 3');
    expect(format(source, { count: 1200, total: 3 })).toBe('1,200 items of 3');
  });

  it('uses the plural rules and number format of the locale', () => {
    const source = '{n, plural, one {# łyżka} few {# łyżki} many {# łyżek} other {# łyżki}}';

    expect([1, 2, 5, 12, 22, 0.5].map((n) => format(source, { n }, 'pl'))).toEqual([
      '1 łyżka',
      '2 łyżki',
      '5 łyżek',
      '12 łyżek',
      '22 łyżki',
      '0,5 łyżki',
    ]);
  });

  it('picks ordinals', () => {
    const source = '{n, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}';
    expect([1, 2, 3, 4, 11, 21].map((n) => format(source, { n }))).toEqual(['1st', '2nd', '3rd', '4th', '11th', '21st']);
  });

  it('falls back to the other case', () => {
    expect(format('{kind, select, a {A} other {Other}}', { kind: 'b' })).toBe('Other');
  });

  it('keeps # as text outside a plural', () => {
    expect(format('No. # {kind, select, other {#}}', { kind: 'x' })).toBe('No. # #');
  });

  it.each([
    ['{count, plural, one {x}}', 'missing the "other" case'],
    ['{count, plural, one {x} other {y}', 'expected a case'],
    ['{count, plural, one {x', 'expected "}"'],
    ['{count, number}', 'unsupported argument type "number"'],
    ['text } more', 'unexpected "}"'],
  ])('rejects %s', (source, problem) => {
    expect(() => parseIcuMessage(source)).toThrow(problem);
  });

  it.each(UI_LANGUAGES)('parses every ICU label in %s', (language) => {
    const icuLabels = labelsOf(translations[language]).filter(isIcuMessage);
    expect(icuLabels.length).toBeGreaterThan(0);
    for (const label of icuLabels) {
      expect(() => parseIcuMessage(label), label).not.toThrow();
    }
  });
});
