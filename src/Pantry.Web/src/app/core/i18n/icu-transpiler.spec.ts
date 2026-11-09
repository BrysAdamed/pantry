import { TestBed } from '@angular/core/testing';
import { provideTransloco, TRANSLOCO_TRANSPILER } from '@jsverse/transloco';
import { IcuTranspiler } from './icu-transpiler';

describe('IcuTranspiler', () => {
  let transpiler: IcuTranspiler;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideTransloco({ config: {} }), { provide: TRANSLOCO_TRANSPILER, useClass: IcuTranspiler }],
    });
    transpiler = TestBed.inject(TRANSLOCO_TRANSPILER) as IcuTranspiler;
  });

  function transpile(value: string, params: Record<string, unknown> = {}): unknown {
    return transpiler.transpile({ value, params, translation: {}, key: 'test' });
  }

  const files = '{count, plural, one {# plik} few {# pliki} many {# plików} other {# pliku}}';

  it('picks the plural form of the active language', () => {
    transpiler.onLangChanged('pl');

    expect([1, 3, 5, 22, 1.5].map((count) => transpile(files, { count }))).toEqual([
      '1 plik',
      '3 pliki',
      '5 plików',
      '22 pliki',
      '1,5 pliku',
    ]);
  });

  it('follows a language change', () => {
    const items = '{count, plural, one {item} other {items}}';
    transpiler.onLangChanged('en');
    expect(transpile(items, { count: 2 })).toBe('items');

    transpiler.onLangChanged('pl');
    expect(transpile(files, { count: 2 })).toBe('2 pliki');
  });

  it('keeps the double brace interpolation of other labels', () => {
    expect(transpile('Rename {{name}}', { name: 'Alex' })).toBe('Rename Alex');
  });

  it('never parses braces typed by a person', () => {
    expect(transpile('Add „{{q}}”', { q: 'a {b' })).toBe('Add „a {b”');
    expect(transpile('{q, select, other {Add {q}}}', { q: '{count, plural' })).toBe('Add {count, plural');
  });
});
