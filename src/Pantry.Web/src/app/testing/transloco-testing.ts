import { EnvironmentProviders, importProvidersFrom, Provider } from '@angular/core';
import { TRANSLOCO_TRANSPILER, TranslocoTestingModule } from '@jsverse/transloco';
import en from '../../../public/i18n/en.json';
import pl from '../../../public/i18n/pl.json';
import { IcuTranspiler } from '../core/i18n/icu-transpiler';
import { DEFAULT_UI_LANGUAGE, UI_LANGUAGES } from '../core/i18n/ui-language';

export const translations = { en, pl };

export function provideTranslocoTesting(): (Provider | EnvironmentProviders)[] {
  return [
    importProvidersFrom(
      TranslocoTestingModule.forRoot({
        langs: translations,
        translocoConfig: {
          availableLangs: [...UI_LANGUAGES],
          defaultLang: DEFAULT_UI_LANGUAGE,
          reRenderOnLangChange: true,
        },
        preloadLangs: true,
      }),
    ),
    { provide: TRANSLOCO_TRANSPILER, useClass: IcuTranspiler },
  ];
}
