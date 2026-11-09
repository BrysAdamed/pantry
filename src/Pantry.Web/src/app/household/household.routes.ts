import { Routes } from '@angular/router';
import { ContentLanguageSection } from './content-language/content-language-section';
import { HouseholdSettings } from './household-settings';
import { MealSlotsSection } from './meal-slots/meal-slots-section';
import { MembersSection } from './members/members-section';
import { PlanningRulesSection } from './planning-rules/planning-rules-section';
import { SectionPlaceholder } from './section-placeholder';
import { SettingsSection } from './settings-sections';

const NOT_BUILT_YET: SettingsSection[] = ['dislikes', 'tokens', 'account'];

export const householdRoutes: Routes = [
  {
    path: '',
    component: HouseholdSettings,
    children: [
      { path: 'members', component: MembersSection },
      { path: 'slots', component: MealSlotsSection },
      { path: 'rules', component: PlanningRulesSection },
      { path: 'language', component: ContentLanguageSection },
      ...NOT_BUILT_YET.map((section) => ({ path: section, component: SectionPlaceholder, data: { section } })),
    ],
  },
];
