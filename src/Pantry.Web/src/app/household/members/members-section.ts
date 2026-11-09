import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, linkedSignal, signal, viewChild } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { Icon } from '../../shared/icon/icon';
import {
  HouseholdApi,
  Member,
  MEMBER_NAME_MAX_LENGTH,
  MEMBERS_URL,
  MembersResponse,
  PORTION_FACTOR,
  roundFactor,
  stepPortionFactor,
} from '../household-api';

export const FACTOR_FORMAT = '1.0-2';

/** Members with their portion factors and the live household serving. */
@Component({
  selector: 'app-members-section',
  imports: [TranslocoDirective, HlmButton, Icon, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './members-section.html',
})
export class MembersSection {
  private readonly api = inject(HouseholdApi);
  private readonly pendingSaves = new Map<string, Promise<unknown>>();

  protected readonly language = inject(UiLanguageService).current;
  protected readonly factorFormat = FACTOR_FORMAT;
  protected readonly bounds = PORTION_FACTOR;
  protected readonly nameMaxLength = MEMBER_NAME_MAX_LENGTH;

  protected readonly household = httpResource<MembersResponse>(() => MEMBERS_URL);
  protected readonly members = linkedSignal<Member[]>(() => this.household.value()?.members ?? []);
  protected readonly householdServing = computed(() =>
    roundFactor(this.members().reduce((sum, member) => sum + member.portionFactor, 0)),
  );

  protected readonly newName = signal('');
  protected readonly canAdd = computed(() => isValidName(this.newName()));
  protected readonly adding = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly saveFailed = signal(false);

  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('nameInput');

  constructor() {
    effect(() => this.nameInput()?.nativeElement.focus());
  }

  /** The first character of the name, kept whole when it is a surrogate pair (an emoji or a rare letter). */
  protected initial(name: string): string {
    return [...name][0] ?? '';
  }

  protected step(id: string, direction: 1 | -1): void {
    const member = this.members().find((m) => m.id === id);
    if (!member) {
      return;
    }
    const portionFactor = stepPortionFactor(member.portionFactor, direction);
    if (portionFactor !== member.portionFactor) {
      this.save({ ...member, portionFactor });
    }
  }

  protected rename(member: Member, value: string): void {
    if (this.editingId() !== member.id) {
      return;
    }
    this.editingId.set(null);
    const name = value.trim();
    if (isValidName(name) && name !== member.name) {
      this.save({ ...member, name });
    }
  }

  protected async add(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canAdd() || this.adding()) {
      return;
    }
    this.adding.set(true);
    try {
      const member = await this.api.addMember({ name: this.newName().trim(), portionFactor: PORTION_FACTOR.default });
      this.members.update((members) => [...members, member]);
      this.newName.set('');
      this.saveFailed.set(false);
    } catch {
      this.saveFailed.set(true);
    } finally {
      this.adding.set(false);
    }
  }

  protected async delete(member: Member): Promise<void> {
    this.deletingId.set(null);
    try {
      await this.api.deleteMember(member.id);
      this.members.update((members) => members.filter((m) => m.id !== member.id));
      this.saveFailed.set(false);
    } catch {
      this.failAndReload();
    }
  }

  /** Shows the change at once and sends saves of one member one after another, so the last change wins. */
  private save(member: Member): void {
    this.members.update((members) => members.map((m) => (m.id === member.id ? member : m)));
    const previous = this.pendingSaves.get(member.id) ?? Promise.resolve();
    const next = previous
      .then(() => this.api.updateMember(member.id, { name: member.name, portionFactor: member.portionFactor }))
      .then(() => this.saveFailed.set(false))
      .catch(() => this.failAndReload())
      .finally(() => {
        if (this.pendingSaves.get(member.id) === next) {
          this.pendingSaves.delete(member.id);
        }
      });
    this.pendingSaves.set(member.id, next);
  }

  private failAndReload(): void {
    this.saveFailed.set(true);
    this.household.reload();
  }
}

function isValidName(value: string): boolean {
  const name = value.trim();
  return name.length > 0 && name.length <= MEMBER_NAME_MAX_LENGTH;
}
