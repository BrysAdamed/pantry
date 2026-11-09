import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UiLanguage } from '../core/i18n/ui-language';

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

  /** Creates the admin. The UI language names the household's default meal slots. */
  completeFirstRun(credential: PublicKeyCredentialJSON, uiLanguage: UiLanguage): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>('/api/setup/complete', { credential, uiLanguage }));
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

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/sign-out', null, handlesUnauthorized()));
  }
}
