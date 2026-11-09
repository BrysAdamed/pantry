import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { CurrentUser, IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { PASSWORD_MAX_LENGTH, USER_NAME_MAX_LENGTH } from '../password/password-rules';
import { safeReturnUrl, Session } from '../session';

export type SignInState = 'ready' | 'signingIn' | 'cancelled' | 'rejected' | 'failed' | 'unsupported';
export type PasswordSignInState = 'ready' | 'signingIn' | 'rejected' | 'failed';

/** Sign-in with a passkey (the button or the username field's autofill) or with a username and password. */
@Component({
  selector: 'app-sign-in',
  imports: [TranslocoDirective, HlmButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-in.html',
})
export class SignIn implements OnInit {
  private readonly api = inject(IdentityApi);
  private readonly passkeys = inject(Passkeys);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  /** Bound from the query string: where to go after sign-in. */
  readonly returnUrl = input<string>();
  /** Bound from the query string: `expired` when a request found the session gone. */
  readonly reason = input<string>();

  protected readonly userNameMaxLength = USER_NAME_MAX_LENGTH;
  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;

  protected readonly state = signal<SignInState>(this.passkeys.isSupported() ? 'ready' : 'unsupported');
  protected readonly passwordState = signal<PasswordSignInState>('ready');
  protected readonly userName = signal('');
  protected readonly password = signal('');
  protected readonly autofill = signal(false);
  protected readonly busy = computed(() => this.state() === 'signingIn' || this.passwordState() === 'signingIn');
  protected readonly isRetryable = computed(() => ['cancelled', 'rejected', 'failed'].includes(this.state()));
  protected readonly canSignIn = computed(() => !this.busy() && (this.state() === 'ready' || this.isRetryable()));
  protected readonly canSignInWithPassword = computed(
    () => !this.busy() && this.userName().trim().length > 0 && this.password().length > 0,
  );
  /** One message at a time: the latest password attempt's, else the passkey's. */
  protected readonly error = computed(() => {
    const password = this.passwordState();
    if (password === 'rejected' || password === 'failed') {
      return `password.${password}`;
    }
    return this.isRetryable() || this.state() === 'unsupported' ? this.state() : null;
  });
  protected readonly expired = computed(() => this.reason() === 'expired' && !this.error());

  private autofillRequest?: AbortController;
  private autofillOptions?: Promise<PublicKeyCredentialRequestOptionsJSON>;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.autofillRequest?.abort());
  }

  async ngOnInit(): Promise<void> {
    if (this.state() === 'ready' && (await this.passkeys.isAutofillSupported())) {
      this.autofill.set(true);
      void this.offerAutofill();
    }
  }

  protected async signIn(): Promise<void> {
    if (!this.canSignIn()) {
      return;
    }
    this.autofillRequest?.abort();
    this.passwordState.set('ready');
    this.state.set('signingIn');
    try {
      await this.autofillOptions?.catch(() => undefined);
      await this.complete(await this.api.signIn(await this.passkeys.get(await this.requestOptions())));
    } catch (error) {
      this.state.set(stateFor(error));
      this.reofferAutofill();
    }
  }

  protected async signInWithPassword(event: Event): Promise<void> {
    event.preventDefault();
    if (!this.canSignInWithPassword()) {
      return;
    }
    this.autofillRequest?.abort();
    if (this.isRetryable()) {
      this.state.set('ready');
    }
    this.passwordState.set('signingIn');
    try {
      await this.api.refreshAntiforgeryToken();
      await this.complete(await this.api.signInWithPassword(this.userName().trim(), this.password()));
    } catch (error) {
      this.passwordState.set(
        error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized ? 'rejected' : 'failed',
      );
      this.reofferAutofill();
    }
  }

  /** Conditional mediation: the browser offers the saved passkeys in the username field's autofill while the screen is open. */
  private async offerAutofill(): Promise<void> {
    const request = new AbortController();
    this.autofillRequest = request;
    this.autofillOptions = this.requestOptions();
    let credential: PublicKeyCredentialJSON;
    try {
      credential = await this.passkeys.get(await this.autofillOptions, { conditional: true, signal: request.signal });
    } catch {
      return;
    }
    this.passwordState.set('ready');
    this.state.set('signingIn');
    try {
      await this.complete(await this.api.signIn(credential));
    } catch (error) {
      this.state.set(stateFor(error));
    }
  }

  private reofferAutofill(): void {
    if (this.autofill()) {
      void this.offerAutofill();
    }
  }

  private async requestOptions(): Promise<PublicKeyCredentialRequestOptionsJSON> {
    await this.api.refreshAntiforgeryToken();
    return this.api.signInPasskeyOptions();
  }

  private async complete(user: CurrentUser): Promise<void> {
    this.session.signedIn(user);
    await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()), { replaceUrl: true });
  }
}

function stateFor(error: unknown): SignInState {
  if (error instanceof PasskeyCancelledError) {
    return 'cancelled';
  }
  if (error instanceof PasskeyUnsupportedError) {
    return 'unsupported';
  }
  if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized) {
    return 'rejected';
  }
  return 'failed';
}
