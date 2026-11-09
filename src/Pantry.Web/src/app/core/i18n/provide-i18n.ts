import { registerLocaleData } from '@angular/common';
import localePl from '@angular/common/locales/pl';
import { EnvironmentProviders, inject, isDevMode, provideAppInitializer, Provider } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { TranslationLoader } from './translation-loader';
import { DEFAULT_UI_LANGUAGE, UI_LANGUAGES } from './ui-language';
import { UiLanguageService } from './ui-language.service';

/** Locale data for Angular's number and date pipes, called with the UI language as the locale. `en` is built in. */
export function registerUiLocales(): void {
  registerLocaleData(localePl, 'pl');
}

export function provideI18n(): (Provider | EnvironmentProviders)[] {
  registerUiLocales();
  return [
    provideTransloco({
      config: {
        availableLangs: [...UI_LANGUAGES],
        defaultLang: DEFAULT_UI_LANGUAGE,
        fallbackLang: DEFAULT_UI_LANGUAGE,
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
        missingHandler: { useFallbackTranslation: true, logMissingKey: isDevMode() },
      },
      loader: TranslationLoader,
    }),
    provideAppInitializer(() => inject(UiLanguageService).init()),
  ];
}
