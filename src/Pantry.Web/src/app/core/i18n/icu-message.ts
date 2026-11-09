type SelectKind = 'plural' | 'selectordinal' | 'select';

type IcuNode =
  | { readonly kind: 'text'; readonly text: string }
  | { readonly kind: 'argument'; readonly name: string }
  | { readonly kind: 'count' }
  | { readonly kind: SelectKind; readonly name: string; readonly cases: ReadonlyMap<string, IcuMessage> };

/** A parsed label in the ICU subset the app uses: `{arg}`, `plural`, `selectordinal`, `select`, `=N` cases and `#`. */
export type IcuMessage = readonly IcuNode[];

const SELECT_KINDS: readonly string[] = ['plural', 'selectordinal', 'select'];
const OTHER = 'other';
const NAME = /\s*([\w.]+)\s*/y;
const CASE_KEY = /\s*(=\d+(?:\.\d+)?|\w+)\s*/y;
const SPACES = /(\s*)/y;

export function isIcuMessage(label: string): boolean {
  return /\{\s*[\w.]+\s*,\s*(plural|selectordinal|select)\s*,/.test(label);
}

export function parseIcuMessage(source: string): IcuMessage {
  let position = 0;

  const fail = (problem: string) => new Error(`ICU message "${source}": ${problem} at ${position}.`);

  const read = (pattern: RegExp, what: string): string => {
    pattern.lastIndex = position;
    const match = pattern.exec(source);
    if (!match) {
      throw fail(`expected ${what}`);
    }
    position = pattern.lastIndex;
    return match[1];
  };

  const expect = (char: string) => {
    if (source[position] !== char) {
      throw fail(`expected "${char}"`);
    }
    position++;
  };

  const message = (inPlural: boolean, nested: boolean): IcuMessage => {
    const nodes: IcuNode[] = [];
    let text = '';
    const flush = () => {
      if (text) {
        nodes.push({ kind: 'text', text });
        text = '';
      }
    };
    while (position < source.length && source[position] !== '}') {
      const char = source[position];
      if (char === '{') {
        flush();
        position++;
        nodes.push(argument(inPlural));
      } else if (char === '#' && inPlural) {
        flush();
        position++;
        nodes.push({ kind: 'count' });
      } else {
        text += char;
        position++;
      }
    }
    if (!nested && position < source.length) {
      throw fail('unexpected "}"');
    }
    flush();
    return nodes;
  };

  const argument = (inPlural: boolean): IcuNode => {
    const name = read(NAME, 'an argument name');
    if (source[position] === '}') {
      position++;
      return { kind: 'argument', name };
    }
    expect(',');
    const kind = read(NAME, 'plural, selectordinal or select');
    if (!SELECT_KINDS.includes(kind)) {
      throw fail(`unsupported argument type "${kind}"`);
    }
    expect(',');
    const cases = new Map<string, IcuMessage>();
    while (source[position] !== '}') {
      const key = read(CASE_KEY, 'a case');
      expect('{');
      cases.set(key, message(kind !== 'select' || inPlural, true));
      expect('}');
      read(SPACES, 'spaces');
    }
    if (!cases.has(OTHER)) {
      throw fail('missing the "other" case');
    }
    position++;
    return { kind: kind as SelectKind, name, cases };
  };

  return message(false, false);
}

export function formatIcuMessage(message: IcuMessage, values: Readonly<Record<string, unknown>>, locale: string): string {
  const render = (nodes: IcuMessage, count: number | null): string =>
    nodes
      .map((node) => {
        switch (node.kind) {
          case 'text':
            return node.text;
          case 'argument':
            return String(values[node.name] ?? '');
          case 'count':
            return count === null ? '#' : new Intl.NumberFormat(locale).format(count);
          case 'select':
            return render(caseOf(node.cases, String(values[node.name] ?? '')), count);
          case 'plural':
          case 'selectordinal': {
            const value = Number(values[node.name] ?? 0);
            const type = node.kind === 'plural' ? 'cardinal' : 'ordinal';
            const chosen =
              node.cases.get(`=${value}`) ?? caseOf(node.cases, new Intl.PluralRules(locale, { type }).select(value));
            return render(chosen, value);
          }
        }
      })
      .join('');
  return render(message, null);
}

function caseOf(cases: ReadonlyMap<string, IcuMessage>, key: string): IcuMessage {
  return cases.get(key) ?? cases.get(OTHER)!;
}
