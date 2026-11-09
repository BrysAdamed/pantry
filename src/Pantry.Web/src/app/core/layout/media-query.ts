import { DestroyRef, inject, signal, Signal } from '@angular/core';

/** The rail breakpoint of the shell (`md`, 768px). */
export const DESKTOP_QUERY = '(min-width: 48rem)';

/** Whether a media query matches, kept up to date. Call in an injection context. */
export function mediaQuery(query: string): Signal<boolean> {
  const list = typeof matchMedia === 'function' ? matchMedia(query) : null;
  const matches = signal(list?.matches ?? false);
  if (list) {
    const listener = (event: MediaQueryListEvent) => matches.set(event.matches);
    list.addEventListener('change', listener);
    inject(DestroyRef).onDestroy(() => list.removeEventListener('change', listener));
  }
  return matches.asReadonly();
}
