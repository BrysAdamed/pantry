import { EnvironmentProviders, effect, inject, Injectable, provideAppInitializer, signal, untracked } from '@angular/core';
import { UiLanguage } from '../core/i18n/ui-language';
import { UiLanguageService } from '../core/i18n/ui-language.service';
import { ThemePreference } from '../core/theme/theme';
import { ThemeService } from '../core/theme/theme.service';
import { CurrentUser } from '../identity/identity-api';
import { Session } from '../identity/session';
import { AccountApi } from './account-api';

/**
 * UI language and theme of the signed-in user (ADR 0011, 0013). At sign-in the user's saved choice wins over this
 * device's; the device keeps a copy in `localStorage` for the screens before sign-in. A change is applied at once
 * and saved on the user, one save after another, so the last choice is the one stored.
 */
@Injectable({ providedIn: 'root' })
export class UserPreferences {
  private readonly session = inject(Session);
  private readonly api = inject(AccountApi);
  private readonly uiLanguage = inject(UiLanguageService);
  private readonly theme = inject(ThemeService);
  private appliedFor: string | null = null;
  private saves: Promise<unknown> = Promise.resolve();

  /** The last change could not be saved on the user; it still applies on this device. */
  readonly saveFailed = signal(false);

  constructor() {
    effect(() => {
      const user = this.session.user();
      untracked(() => this.applySaved(user));
    });
  }

  setUiLanguage(language: UiLanguage): Promise<void> {
    this.uiLanguage.use(language);
    return this.save(() => this.api.changeUiLanguage(language));
  }

  setTheme(preference: ThemePreference): Promise<void> {
    this.theme.use(preference);
    return this.save(() => this.api.changeTheme(preference));
  }

  /** Once per signed-in user, so a later refresh of the user does not undo a change still being saved. */
  private applySaved(user: CurrentUser | null): void {
    if (user?.id === this.appliedFor) {
      return;
    }
    this.appliedFor = user?.id ?? null;
    if (!user) {
      return;
    }
    if (user.uiLanguage) {
      this.uiLanguage.use(user.uiLanguage);
    }
    this.theme.use(user.theme);
  }

  private save(call: () => Promise<void>): Promise<void> {
    if (!this.session.user()) {
      return Promise.resolve();
    }
    const next = this.saves.then(call).then(
      () => this.saveFailed.set(false),
      () => this.saveFailed.set(true),
    );
    this.saves = next;
    return next;
  }
}

/** Starts applying the signed-in user's preferences. */
export function provideUserPreferences(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(UserPreferences);
  });
}
