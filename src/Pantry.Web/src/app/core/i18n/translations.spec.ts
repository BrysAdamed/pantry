import { translations } from '../../testing/transloco-testing';
import { UI_LANGUAGES } from './ui-language';

function keysOf(value: object, prefix = ''): string[] {
  return Object.entries(value).flatMap(([key, child]) =>
    child !== null && typeof child === 'object' ? keysOf(child, `${prefix}${key}.`) : [`${prefix}${key}`],
  );
}

describe('translations', () => {
  const reference = keysOf(translations.en).sort();

  it.each(UI_LANGUAGES)('%s has exactly the same keys as en', (language) => {
    expect(keysOf(translations[language]).sort()).toEqual(reference);
  });

  it.each(UI_LANGUAGES)('%s has no empty labels', (language) => {
    const empty = keysOf(translations[language]).filter((key) => {
      const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], translations[language]);
      return typeof value !== 'string' || value.trim() === '';
    });
    expect(empty).toEqual([]);
  });
});
