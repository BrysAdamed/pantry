import { DatePipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, linkedSignal, signal, viewChild } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../core/i18n/ui-language.service';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../identity/passkeys';
import { Session } from '../identity/session';
import { Icon } from '../shared/icon/icon';
import { AccountApi, Passkey, PASSKEY_NAME_MAX_LENGTH, PASSKEYS_URL } from './account-api';
import { LanguageSwitcher } from './language-switcher';
import { PasswordSection } from './password-section';
import { ThemeSwitcher } from './theme-switcher';
import { UserPreferences } from './user-preferences';

export const PASSKEY_DATE_FORMAT = 'd MMM y';

export type AddPasskeyState = 'idle' | 'adding' | 'cancelled' | 'failed' | 'unsupported';

/** *My account*: the signed-in user's UI language, theme, passkeys and password, and signing out other devices. */
@Component({
  selector: 'app-account-section',
  imports: [TranslocoDirective, HlmButton, Icon, DatePipe, LanguageSwitcher, ThemeSwitcher, PasswordSection],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'grid content-start gap-4.5' },
  templateUrl: './account-section.html',
})
export class AccountSection {
  private readonly api = inject(AccountApi);
  private readonly passkeySupport = inject(Passkeys);
  private readonly session = inject(Session);

  protected readonly language = inject(UiLanguageService).current;
  protected readonly preferences = inject(UserPreferences);
  protected readonly dateFormat = PASSKEY_DATE_FORMAT;
  protected readonly nameMaxLength = PASSKEY_NAME_MAX_LENGTH;

  protected readonly passkeyList = httpResource<Passkey[]>(() => PASSKEYS_URL);
  protected readonly passkeys = linkedSignal<Passkey[]>(() => this.passkeyList.value() ?? []);
  protected readonly hasPassword = computed(() => this.session.user()?.signInMethods.includes('password') ?? false);
  /** The only passkey of a user without a password: their last sign-in method, which has to stay. */
  protected readonly lastMethod = computed(() => this.passkeys().length === 1 && !this.hasPassword());

  protected readonly addState = signal<AddPasskeyState>('idle');
  protected readonly editingId = signal<string | null>(null);
  protected readonly removingId = signal<string | null>(null);
  protected readonly saveFailed = signal(false);
  protected readonly lastPasskey = signal(false);
  protected readonly signingOutOthers = signal(false);
  protected readonly othersSignedOut = signal<'done' | 'failed' | null>(null);

  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('nameInput');

  constructor() {
    effect(() => this.nameInput()?.nativeElement.focus());
  }

  protected async add(): Promise<void> {
    if (this.addState() === 'adding') {
      return;
    }
    this.addState.set('adding');
    try {
      const credential = await this.passkeySupport.create(await this.api.passkeyOptions());
      const passkey = await this.api.addPasskey(credential);
      this.passkeys.update((passkeys) => [...passkeys, passkey]);
      this.addState.set('idle');
    } catch (error) {
      this.addState.set(addStateFor(error));
    }
  }

  protected async rename(passkey: Passkey, value: string): Promise<void> {
    if (this.editingId() !== passkey.id) {
      return;
    }
    this.editingId.set(null);
    const name = value.trim();
    if (!name || name.length > PASSKEY_NAME_MAX_LENGTH || name === passkey.name) {
      return;
    }
    this.passkeys.update((passkeys) => passkeys.map((p) => (p.id === passkey.id ? { ...p, name } : p)));
    try {
      await this.api.renamePasskey(passkey.id, name);
      this.saveFailed.set(false);
    } catch {
      this.failAndReload();
    }
  }

  protected async remove(passkey: Passkey): Promise<void> {
    this.removingId.set(null);
    this.lastPasskey.set(false);
    try {
      await this.api.removePasskey(passkey.id);
      this.passkeys.update((passkeys) => passkeys.filter((p) => p.id !== passkey.id));
      this.saveFailed.set(false);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict) {
        this.lastPasskey.set(true);
        this.passkeyList.reload();
        void this.session.refresh();
      } else {
        this.failAndReload();
      }
    }
  }

  protected async signOutOthers(): Promise<void> {
    this.signingOutOthers.set(true);
    try {
      await this.api.signOutOtherSessions();
      this.othersSignedOut.set('done');
    } catch {
      this.othersSignedOut.set('failed');
    } finally {
      this.signingOutOthers.set(false);
    }
  }

  private failAndReload(): void {
    this.saveFailed.set(true);
    this.passkeyList.reload();
  }
}

function addStateFor(error: unknown): AddPasskeyState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof PasskeyUnsupportedError) {
    return 'unsupported';
  }
  return 'failed';
}
