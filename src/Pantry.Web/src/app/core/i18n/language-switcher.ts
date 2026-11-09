import { Component, inject } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UI_LANGUAGES } from './ui-language';
import { UiLanguageService } from './ui-language.service';

@Component({
  selector: 'app-language-switcher',
  imports: [TranslocoDirective, HlmButton],
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
          (click)="uiLanguage.use(language)"
        >
          {{ t('language.' + language) }}
        </button>
      }
    </div>
  `,
})
export class LanguageSwitcher {
  protected readonly uiLanguage = inject(UiLanguageService);
  protected readonly languages = UI_LANGUAGES;
}
