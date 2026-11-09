import { Component, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { CurrentUser, IdentityApi, NewCredentials, SignInMethod } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { NewPasswordFields } from '../password/new-password-fields';
import { SignInMethodChoice } from '../password/sign-in-method-choice';
import { Session } from '../session';
import { SetupStatusService } from '../setup-status';

export type FirstRunState = 'form' | 'creating' | 'cancelled' | 'failed' | 'passwordFailed' | 'alreadyDone' | 'done';

export const DISPLAY_NAME_MAX_LENGTH = 50;

/** The first run: the admin's name and either a passkey or a username and password. */
@Component({
  selector: 'app-first-run',
  imports: [TranslocoDirective, HlmButton, SignInMethodChoice, NewPasswordFields],
  templateUrl: './first-run.html',
})
export class FirstRun {
  private readonly api = inject(IdentityApi);
  private readonly passkeys = inject(Passkeys);
  private readonly setupStatus = inject(SetupStatusService);
  private readonly router = inject(Router);
  private readonly session = inject(Session);
  private readonly uiLanguage = inject(UiLanguageService).current;

  protected readonly maxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly passkeysWork = signal(this.passkeys.isSupported());
  protected readonly method = signal<SignInMethod>(this.passkeysWork() ? 'passkey' : 'password');
  protected readonly displayName = signal('');
  protected readonly credentials = signal<NewCredentials | null>(null);
  protected readonly state = signal<FirstRunState>('form');
  protected readonly isRetryable = computed(() => ['cancelled', 'failed', 'passwordFailed'].includes(this.state()));
  protected readonly passkeyUnavailable = computed(
    () => this.method() === 'passkey' && !this.passkeysWork(),
  );
  protected readonly canCreate = computed(
    () =>
      this.displayName().trim().length > 0 &&
      (this.state() === 'form' || this.isRetryable()) &&
      (this.method() === 'passkey' ? !this.passkeyUnavailable() : this.credentials() !== null),
  );
  protected readonly showsForm = computed(() => this.state() !== 'done' && this.state() !== 'alreadyDone');
  protected readonly error = computed(() => (this.passkeyUnavailable() ? 'unsupported' : this.isRetryable() ? this.state() : null));

  /** The password fields start empty each time they are shown, so credentials typed earlier are dropped. */
  protected chooseMethod(method: SignInMethod): void {
    this.method.set(method);
    this.credentials.set(null);
    if (this.isRetryable()) {
      this.state.set('form');
    }
  }

  protected async create(): Promise<void> {
    if (!this.canCreate()) {
      return;
    }
    const method = this.method();
    this.state.set('creating');
    try {
      this.session.signedIn(method === 'passkey' ? await this.createPasskey() : await this.createWithPassword());
      this.setupStatus.markDone();
      this.state.set('done');
    } catch (error) {
      if (error instanceof PasskeyUnsupportedError) {
        this.passkeysWork.set(false);
        this.state.set('form');
      } else {
        this.state.set(stateFor(error, method));
      }
    }
  }

  protected openApp(): void {
    this.setupStatus.markDone();
    void this.router.navigateByUrl('/');
  }

  private async createPasskey(): Promise<CurrentUser> {
    const options = await this.api.firstRunPasskeyOptions(this.displayName().trim());
    const credential = await this.passkeys.create(options);
    return this.api.completeFirstRun(credential, this.uiLanguage());
  }

  private createWithPassword(): Promise<CurrentUser> {
    return this.api.completeFirstRunWithPassword(this.displayName().trim(), this.credentials()!, this.uiLanguage());
  }
}

function stateFor(error: unknown, method: SignInMethod): FirstRunState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Conflict) {
    return 'alreadyDone';
  }
  return method === 'password' ? 'passwordFailed' : 'failed';
}
