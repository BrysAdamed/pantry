import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { CurrentUser, NewCredentials } from '../identity/identity-api';
import { NewPasswordFields } from '../identity/password/new-password-fields';
import { PASSWORD_MAX_LENGTH } from '../identity/password/password-rules';
import { Session } from '../identity/session';
import { AccountApi } from './account-api';

export type PasswordMode = 'idle' | 'setting' | 'changing' | 'confirmRemove';
export type PasswordOutcome = 'saved' | 'removed' | 'userNameTaken' | 'wrongCurrent' | 'lastMethod' | 'outdated' | 'failed';

/**
 * The password part of *My account*: shows the username, and sets, changes or removes the password. Each change signs
 * out the user's other devices. The password can go only while the user has a passkey.
 */
@Component({
  selector: 'app-password-section',
  imports: [TranslocoDirective, HlmButton, NewPasswordFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './password-section.html',
})
export class PasswordSection {
  private readonly api = inject(AccountApi);
  private readonly session = inject(Session);

  /** Whether the user has a passkey, from the passkey list on the same screen. */
  readonly hasPasskey = input.required<boolean>();

  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;
  protected readonly userName = computed(() => this.session.user()?.userName ?? null);
  protected readonly hasPassword = computed(() => this.session.user()?.signInMethods.includes('password') ?? false);
  protected readonly mode = signal<PasswordMode>('idle');
  protected readonly saving = signal(false);
  protected readonly currentPassword = signal('');
  protected readonly credentials = signal<NewCredentials | null>(null);
  protected readonly outcome = signal<PasswordOutcome | null>(null);
  protected readonly canSave = computed(
    () => !this.saving() && this.credentials() !== null && (this.mode() !== 'changing' || this.currentPassword().length > 0),
  );

  protected start(mode: PasswordMode): void {
    this.credentials.set(null);
    this.currentPassword.set('');
    this.outcome.set(null);
    this.mode.set(mode);
  }

  protected async save(event: Event): Promise<void> {
    event.preventDefault();
    const credentials = this.credentials();
    if (!this.canSave() || !credentials) {
      return;
    }
    await this.run(
      () =>
        this.mode() === 'changing'
          ? this.api.changePassword(this.currentPassword(), credentials.password)
          : this.api.setPassword(credentials),
      'saved',
      'outdated',
    );
  }

  protected async remove(): Promise<void> {
    await this.run(() => this.api.removePassword(), 'removed', 'lastMethod');
  }

  /** Runs a change. A 409 means the user changed meanwhile, on another device or by a rule: they are read again. */
  private async run(change: () => Promise<CurrentUser>, done: PasswordOutcome, conflict: PasswordOutcome): Promise<void> {
    this.saving.set(true);
    this.outcome.set(null);
    try {
      this.session.changed(await change());
      this.mode.set('idle');
      this.outcome.set(done);
    } catch (error) {
      const outcome = outcomeFor(error, conflict);
      this.outcome.set(outcome);
      if (outcome === conflict) {
        this.mode.set('idle');
        void this.session.refresh();
      }
    } finally {
      this.saving.set(false);
    }
  }
}

function outcomeFor(error: unknown, conflict: PasswordOutcome): PasswordOutcome {
  if (error instanceof HttpErrorResponse) {
    if (error.status === HttpStatusCode.BadRequest && error.error?.errors?.UserName) {
      return 'userNameTaken';
    }
    if (error.status === HttpStatusCode.BadRequest && error.error?.errors?.CurrentPassword) {
      return 'wrongCurrent';
    }
    if (error.status === HttpStatusCode.Conflict) {
      return conflict;
    }
  }
  return 'failed';
}
