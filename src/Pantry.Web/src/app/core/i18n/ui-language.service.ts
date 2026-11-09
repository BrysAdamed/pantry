import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, Observable } from 'rxjs';
import { DEFAULT_UI_LANGUAGE, isUiLanguage, UiLanguage } from './ui-language';

const STORAGE_KEY = 'pantry.uiLanguage';

/**
 * The UI language of this device, kept in `localStorage` for the screens before sign-in.
 * After sign-in `UserPreferences` sets it from the user's saved choice.
 */
@Injectable({ providedIn: 'root' })
export class UiLanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly title = inject(Title);

  readonly current = toSignal(this.transloco.langChanges$ as Observable<UiLanguage>, {
    initialValue: this.transloco.getActiveLang() as UiLanguage,
  });

  async init(): Promise<void> {
    const language = this.preferred();
    this.use(language);
    this.transloco.selectTranslate('app.name').subscribe((name) => this.title.setTitle(name));
    await firstValueFrom(this.transloco.load(language)).catch(() => undefined);
  }

  use(language: UiLanguage): void {
    this.transloco.setActiveLang(language);
    this.document.documentElement.lang = language;
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Storage can be unavailable (private mode); the choice then lasts for the session only.
    }
  }

  private preferred(): UiLanguage {
    let stored: string | null = null;
    try {
      stored = localStorage.getItem(STORAGE_KEY);
    } catch {
      stored = null;
    }
    if (isUiLanguage(stored)) {
      return stored;
    }
    const browser = navigator.language?.slice(0, 2).toLowerCase();
    return isUiLanguage(browser) ? browser : DEFAULT_UI_LANGUAGE;
  }
}
