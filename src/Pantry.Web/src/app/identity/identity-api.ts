import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SetupStatus {
  firstRunNeeded: boolean;
}

export interface CurrentUser {
  id: string;
  displayName: string;
  role: 'admin' | 'user';
  uiLanguage: 'pl' | 'en' | null;
  theme: 'system' | 'light' | 'dark';
}

export type PasskeyLinkKind = 'invite' | 'recovery';

/** An invite or recovery link as its redeem screen shows it. */
export interface RedeemableLink {
  kind: PasskeyLinkKind;
  /** The member's name (invite) or the user's display name (recovery). */
  name: string;
  invitedBy: string | null;
  expiresAt: string;
}

/** Marks a request whose 401 the caller handles itself, so the auth interceptor does not send the user to sign-in. */
export const HANDLES_UNAUTHORIZED = new HttpContextToken<boolean>(() => false);

const handlesUnauthorized = () => ({ context: new HttpContext().set(HANDLES_UNAUTHORIZED, true) });

/** Calls of the Identity module (`/api/setup`, `/api/auth`, `/api/me`). */
@Injectable({ providedIn: 'root' })
export class IdentityApi {
  private readonly http = inject(HttpClient);

  setupStatus(): Promise<SetupStatus> {
    return firstValueFrom(this.http.get<SetupStatus>('/api/setup'));
  }

  firstRunPasskeyOptions(displayName: string): Promise<PublicKeyCredentialCreationOptionsJSON> {
    return firstValueFrom(
      this.http.post<PublicKeyCredentialCreationOptionsJSON>('/api/setup/passkey-options', { displayName }),
    );
  }

  completeFirstRun(credential: PublicKeyCredentialJSON): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>('/api/setup/complete', { credential }));
  }

  /** The signed-in user. Fails with 401 when there is no session. */
  me(): Promise<CurrentUser> {
    return firstValueFrom(this.http.get<CurrentUser>('/api/me', handlesUnauthorized()));
  }

  /** Refreshes the antiforgery token for whoever the browser is now; a token from an ended session would be rejected. */
  async refreshAntiforgeryToken(): Promise<void> {
    await firstValueFrom(this.http.get('/api/antiforgery', handlesUnauthorized()));
  }

  signInPasskeyOptions(): Promise<PublicKeyCredentialRequestOptionsJSON> {
    return firstValueFrom(
      this.http.post<PublicKeyCredentialRequestOptionsJSON>('/api/auth/passkey-options', null, handlesUnauthorized()),
    );
  }

  signIn(credential: PublicKeyCredentialJSON): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>('/api/auth/sign-in', { credential }, handlesUnauthorized()));
  }

  redeemableLink(token: string): Promise<RedeemableLink> {
    return firstValueFrom(this.http.get<RedeemableLink>(redeemUrl(token), handlesUnauthorized()));
  }

  redeemPasskeyOptions(token: string, displayName: string): Promise<PublicKeyCredentialCreationOptionsJSON> {
    return firstValueFrom(
      this.http.post<PublicKeyCredentialCreationOptionsJSON>(`${redeemUrl(token)}/passkey-options`, { displayName }, handlesUnauthorized()),
    );
  }

  completeRedeem(token: string, credential: PublicKeyCredentialJSON): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>(`${redeemUrl(token)}/complete`, { credential }, handlesUnauthorized()));
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/sign-out', null, handlesUnauthorized()));
  }
}

function redeemUrl(token: string): string {
  return `/api/passkey-links/redeem/${encodeURIComponent(token)}`;
}
