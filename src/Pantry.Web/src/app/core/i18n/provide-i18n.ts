import { EnvironmentProviders, inject, isDevMode, provideAppInitializer, Provider } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { TranslationLoader } from './translation-loader';
import { DEFAULT_UI_LANGUAGE, UI_LANGUAGES } from './ui-language';
import { UiLanguageService } from './ui-language.service';

export function provideI18n(): (Provider | EnvironmentProviders)[] {
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
