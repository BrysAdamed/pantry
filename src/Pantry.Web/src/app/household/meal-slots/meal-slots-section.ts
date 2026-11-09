import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { Icon } from '../../shared/icon/icon';
import { Switch } from '../../shared/switch/switch';
import { HouseholdApi, MEAL_SLOT_NAME_MAX_LENGTH, MEAL_SLOTS_URL, MealSlot } from '../household-api';

/** Meal slots in planner order: rename in place, arrows to reorder, a switch to take a slot out of the plan. */
@Component({
  selector: 'app-meal-slots-section',
  imports: [TranslocoDirective, HlmButton, Icon, Switch],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './meal-slots-section.html',
})
export class MealSlotsSection {
  private readonly api = inject(HouseholdApi);
  private writes: Promise<unknown> = Promise.resolve();

  protected readonly nameMaxLength = MEAL_SLOT_NAME_MAX_LENGTH;

  protected readonly resource = httpResource<MealSlot[]>(() => MEAL_SLOTS_URL);
  protected readonly slots = linkedSignal<MealSlot[]>(() => this.resource.value() ?? []);
  protected readonly enabledCount = computed(() => this.slots().filter((slot) => slot.isEnabled).length);

  protected readonly newName = signal('');
  protected readonly canAdd = computed(() => isValidName(this.newName()));
  protected readonly adding = signal(false);
  protected readonly saveFailed = signal(false);

  protected rename(slot: MealSlot, input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!isValidName(name) || name === slot.name) {
      input.value = slot.name;
      return;
    }
    this.replace({ ...slot, name });
    this.write(() => this.api.renameMealSlot(slot.id, name));
  }

  protected move(id: string, direction: 1 | -1): void {
    const slots = [...this.slots()];
    const index = slots.findIndex((slot) => slot.id === id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= slots.length) {
      return;
    }
    [slots[index], slots[target]] = [slots[target], slots[index]];
    const ordered = slots.map((slot, position) => ({ ...slot, position }));
    this.slots.set(ordered);
    this.write(() => this.api.reorderMealSlots(ordered.map((slot) => slot.id)));
  }

  protected setEnabled(slot: MealSlot, isEnabled: boolean): void {
    if (!isEnabled && this.enabledCount() <= 1) {
      return;
    }
    this.replace({ ...slot, isEnabled });
    this.write(() => this.api.setMealSlotEnabled(slot.id, isEnabled));
  }

  protected async add(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canAdd() || this.adding()) {
      return;
    }
    this.adding.set(true);
    try {
      const slot = await this.api.addMealSlot(this.newName().trim());
      this.slots.update((slots) => [...slots, slot]);
      this.newName.set('');
      this.saveFailed.set(false);
    } catch {
      this.failAndReload();
    } finally {
      this.adding.set(false);
    }
  }

  private replace(slot: MealSlot): void {
    this.slots.update((slots) => slots.map((s) => (s.id === slot.id ? slot : s)));
  }

  /** Shows changes at once and sends them one after another, so the server sees them in the order they were made. */
  private write(send: () => Promise<unknown>): void {
    this.writes = this.writes
      .then(send)
      .then(() => this.saveFailed.set(false))
      .catch(() => this.failAndReload());
  }

  private failAndReload(): void {
    this.saveFailed.set(true);
    this.resource.reload();
  }
}

function isValidName(value: string): boolean {
  const name = value.trim();
  return name.length > 0 && name.length <= MEAL_SLOT_NAME_MAX_LENGTH;
}
