import { HttpClient, HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SignInMethod } from '../identity/identity-api';

export const MEMBERS_URL = '/api/household/members';

export const PORTION_FACTOR = { min: 0.05, max: 2, step: 0.05, default: 1 } as const;
export const MEMBER_NAME_MAX_LENGTH = 50;

export interface Member {
  id: string;
  name: string;
  portionFactor: number;
  position: number;
  account: 'admin' | 'user' | null;
  userId: string | null;
  /** How the linked user signs in; null without an account. */
  signInMethods: SignInMethod[] | null;
}

export interface MembersResponse {
  members: Member[];
  householdServing: number;
}

export const MEAL_SLOTS_URL = '/api/household/meal-slots';
export const MEAL_SLOT_NAME_MAX_LENGTH = 50;

export interface MealSlot {
  id: string;
  name: string;
  position: number;
  isEnabled: boolean;
}

export const PLANNING_RULES_URL = '/api/household/planning-rules';
export const PLANNING_RULE_TEXT_MAX_LENGTH = 200;

export interface PlanningRule {
  id: string;
  text: string;
  position: number;
  isEnabled: boolean;
}

export const DISLIKES_URL = '/api/household/dislikes';
export const DISLIKE_REASON_MAX_LENGTH = 100;

/** `never`: an allergy or a hard no; `avoid`: better not. Never comes first wherever both are shown. */
export const DISLIKE_KINDS = ['never', 'avoid'] as const;

export type DislikeKind = (typeof DISLIKE_KINDS)[number];

/** A household dislike of a catalog ingredient; only a `never` dislike has a reason. */
export interface Dislike {
  id: string;
  ingredientId: string;
  ingredientName: string;
  kind: DislikeKind;
  reason: string | null;
}

export interface DislikeChange {
  kind: DislikeKind;
  reason: string | null;
}

/** The id of the dislike the ingredient already has, from a 409 of `addDislike`; null for any other error. */
export function existingDislikeOf(error: unknown): string | null {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict && typeof error.error?.dislikeId === 'string'
    ? error.error.dislikeId
    : null;
}

export const CONTENT_LANGUAGE_URL = '/api/household/content-language';

/** BCP 47 codes the API accepts, in the order the picker shows them. */
export const CONTENT_LANGUAGES = ['pl', 'en', 'de', 'fr', 'es', 'it', 'cs', 'uk'] as const;

export type ContentLanguage = (typeof CONTENT_LANGUAGES)[number];

export interface ContentLanguageSetting {
  contentLanguage: ContentLanguage;
}

export interface MemberChange {
  name: string;
  portionFactor: number;
}

/** Writes of the Household module. Reads use `httpResource` in the sections. */
@Injectable({ providedIn: 'root' })
export class HouseholdApi {
  private readonly http = inject(HttpClient);

  addMember(change: MemberChange): Promise<Member> {
    return firstValueFrom(this.http.post<Member>(MEMBERS_URL, change));
  }

  updateMember(id: string, change: MemberChange): Promise<Member> {
    return firstValueFrom(this.http.put<Member>(`${MEMBERS_URL}/${id}`, change));
  }

  deleteMember(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${MEMBERS_URL}/${id}`));
  }

  addMealSlot(name: string): Promise<MealSlot> {
    return firstValueFrom(this.http.post<MealSlot>(MEAL_SLOTS_URL, { name }));
  }

  renameMealSlot(id: string, name: string): Promise<MealSlot> {
    return firstValueFrom(this.http.put<MealSlot>(`${MEAL_SLOTS_URL}/${id}/name`, { name }));
  }

  setMealSlotEnabled(id: string, isEnabled: boolean): Promise<MealSlot> {
    return firstValueFrom(this.http.put<MealSlot>(`${MEAL_SLOTS_URL}/${id}/enabled`, { isEnabled }));
  }

  /** Lists every meal slot id, disabled ones included, in the new order. */
  reorderMealSlots(mealSlotIds: string[]): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${MEAL_SLOTS_URL}/order`, { mealSlotIds }));
  }

  addPlanningRule(text: string): Promise<PlanningRule> {
    return firstValueFrom(this.http.post<PlanningRule>(PLANNING_RULES_URL, { text }));
  }

  editPlanningRule(id: string, text: string): Promise<PlanningRule> {
    return firstValueFrom(this.http.put<PlanningRule>(`${PLANNING_RULES_URL}/${id}/text`, { text }));
  }

  setPlanningRuleEnabled(id: string, isEnabled: boolean): Promise<PlanningRule> {
    return firstValueFrom(this.http.put<PlanningRule>(`${PLANNING_RULES_URL}/${id}/enabled`, { isEnabled }));
  }

  /** Lists every planning rule id, disabled ones included, in the new order. */
  reorderPlanningRules(planningRuleIds: string[]): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${PLANNING_RULES_URL}/order`, { planningRuleIds }));
  }

  deletePlanningRule(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${PLANNING_RULES_URL}/${id}`));
  }

  /** Answers 409 when the ingredient already has a dislike (`existingDislikeOf`). */
  addDislike(ingredientId: string, change: DislikeChange): Promise<Dislike> {
    return firstValueFrom(this.http.post<Dislike>(DISLIKES_URL, { ingredientId, ...change }));
  }

  changeDislike(id: string, change: DislikeChange): Promise<Dislike> {
    return firstValueFrom(this.http.put<Dislike>(`${DISLIKES_URL}/${id}`, change));
  }

  deleteDislike(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${DISLIKES_URL}/${id}`));
  }

  setContentLanguage(contentLanguage: ContentLanguage): Promise<ContentLanguageSetting> {
    return firstValueFrom(this.http.put<ContentLanguageSetting>(CONTENT_LANGUAGE_URL, { contentLanguage }));
  }
}

/** Rounds to whole hundredths, so sums and steps of factors do not drift (0.1 + 0.2). */
export function roundFactor(value: number): number {
  return Math.round(value * 100) / 100;
}

export function stepPortionFactor(factor: number, direction: 1 | -1): number {
  const next = roundFactor(factor + direction * PORTION_FACTOR.step);
  return Math.min(PORTION_FACTOR.max, Math.max(PORTION_FACTOR.min, next));
}
