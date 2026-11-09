import { Routes } from '@angular/router';
import { HouseholdSettings } from './household-settings';
import { MealSlotsSection } from './meal-slots/meal-slots-section';
import { MembersSection } from './members/members-section';
import { SectionPlaceholder } from './section-placeholder';
import { SettingsSection } from './settings-sections';

const NOT_BUILT_YET: SettingsSection[] = ['dislikes', 'rules', 'language', 'tokens', 'account'];

export const householdRoutes: Routes = [
  {
    path: '',
    component: HouseholdSettings,
    children: [
      { path: 'members', component: MembersSection },
      { path: 'slots', component: MealSlotsSection },
      ...NOT_BUILT_YET.map((section) => ({ path: section, component: SectionPlaceholder, data: { section } })),
    ],
  },
];
