import { IconName } from '../shared/icon/icon';

export const DESTINATIONS = ['planner', 'recipes', 'shopping', 'ingredients', 'household'] as const;

export type Destination = (typeof DESTINATIONS)[number];

export const DESTINATION_ICONS: Record<Destination, IconName> = {
  planner: 'calendar',
  recipes: 'book',
  shopping: 'cart',
  ingredients: 'carrot',
  household: 'home',
};
