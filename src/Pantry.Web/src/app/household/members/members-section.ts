import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, linkedSignal, signal, viewChild } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { PasskeyLinkKind } from '../../identity/identity-api';
import { Session } from '../../identity/session';
import { OpenPasskeyLink, PASSKEY_LINKS_URL, passkeyLinkUrl, UserAdminApi } from '../../identity/user-admin-api';
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

/** A link the admin just issued, shown once with its address to copy. */
export interface IssuedLink {
  kind: PasskeyLinkKind;
  name: string;
  url: string;
}

/** Members with their portion factors and the live household serving. Admins also manage accounts here. */
@Component({
  selector: 'app-members-section',
  imports: [TranslocoDirective, HlmButton, Icon, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './members-section.html',
})
export class MembersSection {
  private readonly api = inject(HouseholdApi);
  private readonly userAdmin = inject(UserAdminApi);
  private readonly session = inject(Session);
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

  protected readonly isAdmin = computed(() => this.session.user()?.role === 'admin');
  protected readonly myUserId = computed(() => this.session.user()?.id ?? null);
  protected readonly openLinks = httpResource<OpenPasskeyLink[]>(() => (this.isAdmin() ? PASSKEY_LINKS_URL : undefined));
  protected readonly openLinkByMember = computed(() => {
    const links = this.openLinks.value() ?? [];
    return new Map(
      this.members().flatMap((member) => {
        const link = links.find((l) => (member.userId ? l.userId === member.userId : l.memberId === member.id));
        return link ? [[member.id, link] as const] : [];
      }),
    );
  });

  protected readonly newName = signal('');
  protected readonly canAdd = computed(() => isValidName(this.newName()));
  protected readonly adding = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly removingUserOf = signal<string | null>(null);
  protected readonly saveFailed = signal(false);
  protected readonly lastAdmin = signal(false);
  protected readonly outdated = signal(false);
  protected readonly issued = signal<IssuedLink | null>(null);
  protected readonly copied = signal(false);

  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('nameInput');
  private readonly issuedUrl = viewChild<ElementRef<HTMLInputElement>>('issuedUrl');

  constructor() {
    effect(() => this.nameInput()?.nativeElement.focus());
    effect(() => this.issuedUrl()?.nativeElement.focus());
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

  /** Issues an invite for a member without an account, or a recovery link for a member's user. */
  protected async issueLink(member: Member): Promise<void> {
    await this.administer('outdated', async () => {
      const link = member.userId
        ? await this.userAdmin.issueRecoveryLink(member.userId)
        : await this.userAdmin.issueInvite(member.id);
      this.copied.set(false);
      this.issued.set({ kind: link.kind, name: member.name, url: passkeyLinkUrl(link.token) });
    });
  }

  protected async revokeLink(link: OpenPasskeyLink): Promise<void> {
    await this.administer('outdated', () => this.userAdmin.revokeLink(link.id));
  }

  protected async changeRole(member: Member, role: 'admin' | 'user'): Promise<void> {
    await this.administer('lastAdmin', async () => {
      await this.userAdmin.changeRole(member.userId!, role);
      this.members.update((members) => members.map((m) => (m.id === member.id ? { ...m, account: role } : m)));
    });
  }

  protected async removeUser(member: Member): Promise<void> {
    this.removingUserOf.set(null);
    await this.administer('lastAdmin', async () => {
      await this.userAdmin.removeUser(member.userId!);
      this.members.update((members) => members.map((m) => (m.id === member.id ? { ...m, account: null, userId: null } : m)));
    });
  }

  protected async copyIssuedLink(): Promise<void> {
    const issued = this.issued();
    if (!issued) {
      return;
    }
    try {
      await navigator.clipboard.writeText(issued.url);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
    }
  }

  /**
   * Runs an account action. A 409 means the last admin would go (`lastAdmin`), or the member or link changed
   * meanwhile (`outdated`: the member already has an account, the link was already used), so the list is read again.
   */
  private async administer(conflict: 'lastAdmin' | 'outdated', action: () => Promise<unknown>): Promise<void> {
    this.lastAdmin.set(false);
    this.outdated.set(false);
    try {
      await action();
      this.saveFailed.set(false);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict) {
        this[conflict].set(true);
        if (conflict === 'outdated') {
          this.household.reload();
        }
      } else {
        this.failAndReload();
      }
    }
    this.openLinks.reload();
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
