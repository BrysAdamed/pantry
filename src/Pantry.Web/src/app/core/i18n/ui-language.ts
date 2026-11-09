export const UI_LANGUAGES = ['pl', 'en'] as const;

export type UiLanguage = (typeof UI_LANGUAGES)[number];

export const DEFAULT_UI_LANGUAGE: UiLanguage = 'en';

export function isUiLanguage(value: unknown): value is UiLanguage {
  return typeof value === 'string' && (UI_LANGUAGES as readonly string[]).includes(value);
}
