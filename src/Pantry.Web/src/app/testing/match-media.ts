import { onTestFinished } from 'vitest';

export interface FakeMediaQueries {
  setMatches(matches: boolean): void;
}

/** Replaces `window.matchMedia` for the current test with a complete fake and restores it when the test finishes. */
export function fakeMatchMedia(matches = false): FakeMediaQueries {
  let current = matches;
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  const original = Object.getOwnPropertyDescriptor(window, 'matchMedia');

  window.matchMedia = (query: string) =>
    ({
      get matches() {
        return current;
      },
      media: query,
      onchange: null,
      addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.add(listener),
      removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.delete(listener),
      addListener: (listener: (event: MediaQueryListEvent) => void) => listeners.add(listener),
      removeListener: (listener: (event: MediaQueryListEvent) => void) => listeners.delete(listener),
      dispatchEvent: () => true,
    }) as unknown as MediaQueryList;

  onTestFinished(() => {
    if (original) {
      Object.defineProperty(window, 'matchMedia', original);
    } else {
      delete (window as Partial<Window>).matchMedia;
    }
  });

  return {
    setMatches(value: boolean) {
      current = value;
      listeners.forEach((listener) => listener({ matches: value } as MediaQueryListEvent));
    },
  };
}
