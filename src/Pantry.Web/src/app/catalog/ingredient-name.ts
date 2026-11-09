const FOLDED: Readonly<Record<string, string>> = { ł: 'l', đ: 'd', ø: 'o', ß: 'ss', æ: 'ae', œ: 'oe' };

export interface TextMatch {
  readonly start: number;
  readonly end: number;
}

/**
 * The comparison form of an ingredient name, the same as the API's `IngredientName.KeyOf`: trimmed, single spaces,
 * lower case, without diacritics (`Żółty` and `zolty` share it).
 */
export function ingredientNameKey(value: string): string {
  return folded(cleanIngredientName(value)).text;
}

export function cleanIngredientName(value: string): string {
  return value.trim().replace(/\s+/g, ' ');
}

/** Where `query` appears in `text`, ignoring letter case and diacritics; null when it does not. */
export function findInName(text: string, query: string): TextMatch | null {
  const key = ingredientNameKey(query);
  if (!key) {
    return null;
  }
  const { text: foldedText, origins } = folded(text);
  const at = foldedText.indexOf(key);
  if (at < 0) {
    return null;
  }
  return { start: origins[at], end: origins[at + key.length - 1] + 1 };
}

/** A piece of a name, `matched` when it is the part a search found. */
export interface TextPart {
  readonly text: string;
  readonly matched: boolean;
}

/** `text` split around where `query` matches it (`findInName`), for marking the match. */
export function markMatch(text: string, query: string): TextPart[] {
  const match = findInName(text, query);
  if (!match) {
    return [{ text, matched: false }];
  }
  return [
    { text: text.slice(0, match.start), matched: false },
    { text: text.slice(match.start, match.end), matched: true },
    { text: text.slice(match.end), matched: false },
  ].filter((part) => part.text);
}

function folded(text: string): { text: string; origins: number[] } {
  let result = '';
  const origins: number[] = [];
  for (let index = 0; index < text.length; index++) {
    const lower = text[index].toLowerCase();
    const plain = /\s/.test(lower) ? ' ' : (FOLDED[lower] ?? lower.normalize('NFD').replace(/\p{Mn}/gu, ''));
    result += plain;
    for (let i = 0; i < plain.length; i++) {
      origins.push(index);
    }
  }
  return { text: result, origins };
}
