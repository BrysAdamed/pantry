import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';
import { DEFAULT_THEME_PREFERENCE, isThemePreference, Theme, ThemePreference } from './theme';

const STORAGE_KEY = 'pantry.theme';
const DARK_QUERY = '(prefers-color-scheme: dark)';

/**
 * The colour theme of this device, kept in `localStorage` for the screens before sign-in.
 * After sign-in `UserPreferences` sets it from the user's saved choice.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly darkQuery = this.document.defaultView?.matchMedia?.(DARK_QUERY);
  private readonly systemPrefersDark = signal(this.darkQuery?.matches ?? false);

  readonly preference = signal<ThemePreference>(DEFAULT_THEME_PREFERENCE);

  readonly theme = computed<Theme>(() => {
    const preference = this.preference();
    if (preference !== 'system') {
      return preference;
    }
    return this.systemPrefersDark() ? 'dark' : 'light';
  });

  init(): void {
    this.darkQuery?.addEventListener('change', (event) => {
      this.systemPrefersDark.set(event.matches);
      this.apply();
    });
    this.preference.set(this.stored());
    this.apply();
  }

  use(preference: ThemePreference): void {
    this.preference.set(preference);
    this.apply();
    try {
      localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      // Storage can be unavailable (private mode); the choice then lasts for the session only.
    }
  }

  private apply(): void {
    this.document.documentElement.dataset['theme'] = this.theme();
  }

  private stored(): ThemePreference {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return isThemePreference(stored) ? stored : DEFAULT_THEME_PREFERENCE;
    } catch {
      return DEFAULT_THEME_PREFERENCE;
    }
  }
}
