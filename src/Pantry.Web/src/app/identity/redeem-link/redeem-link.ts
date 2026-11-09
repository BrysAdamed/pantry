import { DatePipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { DISPLAY_NAME_MAX_LENGTH } from '../first-run/first-run';
import { CurrentUser, IdentityApi, NewCredentials, RedeemableLink, SignInMethod } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { NewPasswordFields } from '../password/new-password-fields';
import { SignInMethodChoice } from '../password/sign-in-method-choice';
import { Session, SIGN_IN_PATH } from '../session';

export type DeadLinkState = 'expired' | 'used' | 'revoked' | 'notFound';
export type RedeemLinkState =
  | 'loading'
  | 'loadFailed'
  | 'form'
  | 'creating'
  | 'cancelled'
  | 'failed'
  | 'passwordFailed'
  | 'userNameTaken'
  | 'done'
  | DeadLinkState;

const DEAD_STATES: readonly RedeemLinkState[] = ['expired', 'used', 'revoked', 'notFound'];
const RETRYABLE_STATES: readonly RedeemLinkState[] = ['cancelled', 'failed', 'passwordFailed', 'userNameTaken'];
export const EXPIRY_FORMAT = 'd MMM, HH:mm';

/**
 * Opens an invite or a recovery link: creates the new user, or signs the existing user back in, with a new passkey or a
 * new password, and signs them in.
 */
@Component({
  selector: 'app-redeem-link',
  imports: [TranslocoDirective, HlmButton, DatePipe, SignInMethodChoice, NewPasswordFields],
  templateUrl: './redeem-link.html',
})
export class RedeemLink implements OnInit {
  private readonly api = inject(IdentityApi);
  private readonly passkeys = inject(Passkeys);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  /** Bound from the route: the token from the link. */
  readonly token = input.required<string>();

  protected readonly language = inject(UiLanguageService).current;
  protected readonly expiryFormat = EXPIRY_FORMAT;
  protected readonly maxLength = DISPLAY_NAME_MAX_LENGTH;
  protected readonly link = signal<RedeemableLink | null>(null);
  protected readonly displayName = signal('');
  protected readonly passkeysWork = signal(this.passkeys.isSupported());
  protected readonly method = signal<SignInMethod>(this.passkeysWork() ? 'passkey' : 'password');
  protected readonly credentials = signal<NewCredentials | null>(null);
  protected readonly state = signal<RedeemLinkState>('loading');
  protected readonly isInvite = computed(() => this.link()?.kind === 'invite');
  protected readonly isRetryable = computed(() => RETRYABLE_STATES.includes(this.state()));
  protected readonly isDead = computed(() => DEAD_STATES.includes(this.state()));
  protected readonly showsForm = computed(() => !!this.link() && !this.isDead() && this.state() !== 'done');
  protected readonly passkeyUnavailable = computed(() => this.method() === 'passkey' && !this.passkeysWork());
  protected readonly canCreate = computed(
    () =>
      (this.state() === 'form' || this.isRetryable()) &&
      (!this.isInvite() || isValidDisplayName(this.displayName())) &&
      (this.method() === 'passkey' ? !this.passkeyUnavailable() : this.credentials() !== null),
  );
  protected readonly error = computed(() => (this.passkeyUnavailable() ? 'unsupported' : this.isRetryable() ? this.state() : null));

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    try {
      const link = await this.api.redeemableLink(this.token());
      this.link.set(link);
      this.displayName.set(link.kind === 'invite' ? link.name : '');
      this.state.set('form');
    } catch (error) {
      const state = stateFor(error, this.method());
      this.state.set(DEAD_STATES.includes(state) ? state : 'loadFailed');
    }
  }

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
      await this.api.refreshAntiforgeryToken();
      this.session.signedIn(method === 'passkey' ? await this.createPasskey() : await this.setPassword());
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
    void this.router.navigateByUrl('/', { replaceUrl: true });
  }

  protected goToSignIn(): void {
    void this.router.navigateByUrl(`/${SIGN_IN_PATH}`, { replaceUrl: true });
  }

  private async createPasskey(): Promise<CurrentUser> {
    const options = await this.api.redeemPasskeyOptions(this.token(), this.displayName().trim());
    const credential = await this.passkeys.create(options);
    return this.api.completeRedeem(this.token(), credential, this.language());
  }

  private setPassword(): Promise<CurrentUser> {
    return this.api.completeRedeemWithPassword(this.token(), this.displayName().trim(), this.credentials()!, this.language());
  }
}

function isValidDisplayName(value: string): boolean {
  const name = value.trim();
  return name.length > 0 && name.length <= DISPLAY_NAME_MAX_LENGTH;
}

function stateFor(error: unknown, method: SignInMethod): RedeemLinkState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof HttpErrorResponse) {
    if (error.status === HttpStatusCode.NotFound) {
      return 'notFound';
    }
    if (error.status === HttpStatusCode.Gone) {
      const state = error.error?.state;
      return state === 'expired' || state === 'used' ? state : 'revoked';
    }
    if (error.status === HttpStatusCode.BadRequest && error.error?.errors?.UserName) {
      return 'userNameTaken';
    }
  }
  return method === 'password' ? 'passwordFailed' : 'failed';
}
