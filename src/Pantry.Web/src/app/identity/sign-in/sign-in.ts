import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Router } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { HlmButton } from '@spartan-ng/helm/button';
import { IdentityApi } from '../identity-api';
import { PasskeyCancelledError, Passkeys, PasskeyUnsupportedError } from '../passkeys';
import { safeReturnUrl, Session } from '../session';

export type SignInState = 'ready' | 'signingIn' | 'cancelled' | 'rejected' | 'failed' | 'unsupported';

@Component({
  selector: 'app-sign-in',
  imports: [TranslocoDirective, HlmButton],
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

  protected readonly state = signal<SignInState>(this.passkeys.isSupported() ? 'ready' : 'unsupported');
  protected readonly autofill = signal(false);
  protected readonly isRetryable = computed(() => ['cancelled', 'rejected', 'failed'].includes(this.state()));
  protected readonly showsError = computed(() => this.isRetryable() || this.state() === 'unsupported');
  protected readonly canSignIn = computed(() => this.state() === 'ready' || this.isRetryable());
  protected readonly expired = computed(() => this.reason() === 'expired' && !this.showsError());

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
    this.state.set('signingIn');
    try {
      await this.autofillOptions?.catch(() => undefined);
      await this.complete(await this.passkeys.get(await this.requestOptions()));
    } catch (error) {
      this.state.set(stateFor(error));
      if (this.autofill()) {
        void this.offerAutofill();
      }
    }
  }

  /** Conditional mediation: the browser offers the saved passkeys in the field's autofill while the screen is open. */
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
    this.state.set('signingIn');
    try {
      await this.complete(credential);
    } catch (error) {
      this.state.set(stateFor(error));
    }
  }

  private async requestOptions(): Promise<PublicKeyCredentialRequestOptionsJSON> {
    await this.api.refreshAntiforgeryToken();
    return this.api.signInPasskeyOptions();
  }

  private async complete(credential: PublicKeyCredentialJSON): Promise<void> {
    this.session.signedIn(await this.api.signIn(credential));
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
