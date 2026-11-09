import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UI_LANGUAGES } from '../core/i18n/ui-language';
import { UiLanguageService } from '../core/i18n/ui-language.service';
import { UserPreferences } from './user-preferences';

/** Picks the UI language: on this device at once, and on the signed-in user. */
@Component({
  selector: 'app-language-switcher',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div *transloco="let t" role="group" class="flex gap-1" [attr.aria-label]="t('language.label')">
      @for (language of languages; track language) {
        <button
          hlmBtn
          size="sm"
          [variant]="language === uiLanguage.current() ? 'default' : 'ghost'"
          [attr.aria-pressed]="language === uiLanguage.current()"
          [attr.lang]="language"
          [attr.data-testid]="'language-' + language"
          (click)="preferences.setUiLanguage(language)"
        >
          {{ t('language.' + language) }}
        </button>
      }
    </div>
  `,
})
export class LanguageSwitcher {
  protected readonly uiLanguage = inject(UiLanguageService);
  protected readonly preferences = inject(UserPreferences);
  protected readonly languages = UI_LANGUAGES;
}
