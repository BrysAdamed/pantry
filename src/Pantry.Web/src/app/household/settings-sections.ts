import { IconName } from '../shared/icon/icon';

export const SETTINGS_SECTIONS = ['members', 'slots', 'dislikes', 'rules', 'language', 'tokens', 'account'] as const;

export type SettingsSection = (typeof SETTINGS_SECTIONS)[number];

/** Sections are grouped as in the prototype: household settings, then personal and agent access. */
export const SETTINGS_GROUPS: readonly (readonly SettingsSection[])[] = [
  ['members', 'slots', 'dislikes', 'rules', 'language'],
  ['tokens', 'account'],
];

export const SECTION_ICONS: Record<SettingsSection, IconName> = {
  members: 'users',
  slots: 'clock',
  dislikes: 'ban',
  rules: 'rules',
  language: 'globe',
  tokens: 'key',
  account: 'user',
};

export const DEFAULT_SECTION: SettingsSection = 'members';

export function isSettingsSection(value: string | undefined): value is SettingsSection {
  return SETTINGS_SECTIONS.includes(value as SettingsSection);
}
