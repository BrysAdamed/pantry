import { DatePipe } from '@angular/common';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { UiLanguageService } from '../../core/i18n/ui-language.service';
import { DISPLAY_NAME_MAX_LENGTH } from '../first-run/first-run';
import { IdentityApi, RedeemableLink } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { Session, SIGN_IN_PATH } from '../session';

export type DeadLinkState = 'expired' | 'used' | 'revoked' | 'notFound';
export type RedeemLinkState =
  | 'loading'
  | 'loadFailed'
  | 'form'
  | 'creating'
  | 'cancelled'
  | 'failed'
  | 'unsupported'
  | 'done'
  | DeadLinkState;

const DEAD_STATES: readonly RedeemLinkState[] = ['expired', 'used', 'revoked', 'notFound'];
export const EXPIRY_FORMAT = 'd MMM, HH:mm';

/** Opens an invite or a recovery link: creates a passkey for the new or existing user and signs them in. */
@Component({
  selector: 'app-redeem-link',
  imports: [TranslocoDirective, HlmButton, DatePipe],
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
  protected readonly state = signal<RedeemLinkState>('loading');
  protected readonly isInvite = computed(() => this.link()?.kind === 'invite');
  protected readonly isRetryable = computed(() => this.state() === 'cancelled' || this.state() === 'failed');
  protected readonly isDead = computed(() => DEAD_STATES.includes(this.state()));
  protected readonly showsForm = computed(() => !!this.link() && !this.isDead() && this.state() !== 'done');
  protected readonly canCreate = computed(
    () =>
      (this.state() === 'form' || this.isRetryable()) &&
      (!this.isInvite() || isValidDisplayName(this.displayName())),
  );

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    try {
      const link = await this.api.redeemableLink(this.token());
      this.link.set(link);
      this.displayName.set(link.kind === 'invite' ? link.name : '');
      this.state.set(this.passkeys.isSupported() ? 'form' : 'unsupported');
    } catch (error) {
      const state = stateFor(error);
      this.state.set(state === 'failed' ? 'loadFailed' : state);
    }
  }

  protected async createPasskey(): Promise<void> {
    if (!this.canCreate()) {
      return;
    }
    this.state.set('creating');
    try {
      await this.api.refreshAntiforgeryToken();
      const options = await this.api.redeemPasskeyOptions(this.token(), this.displayName().trim());
      const credential = await this.passkeys.create(options);
      this.session.signedIn(await this.api.completeRedeem(this.token(), credential));
      this.state.set('done');
    } catch (error) {
      this.state.set(stateFor(error));
    }
  }

  protected openApp(): void {
    void this.router.navigateByUrl('/', { replaceUrl: true });
  }

  protected goToSignIn(): void {
    void this.router.navigateByUrl(`/${SIGN_IN_PATH}`, { replaceUrl: true });
  }
}

function isValidDisplayName(value: string): boolean {
  const name = value.trim();
  return name.length > 0 && name.length <= DISPLAY_NAME_MAX_LENGTH;
}

function stateFor(error: unknown): RedeemLinkState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof PasskeyUnsupportedError) {
    return 'unsupported';
  }
  if (error instanceof HttpErrorResponse) {
    if (error.status === HttpStatusCode.NotFound) {
      return 'notFound';
    }
    if (error.status === HttpStatusCode.Gone) {
      const state = error.error?.state;
      return state === 'expired' || state === 'used' ? state : 'revoked';
    }
  }
  return 'failed';
}
