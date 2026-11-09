import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UiLanguage } from '../core/i18n/ui-language';
import { ThemePreference } from '../core/theme/theme';
import { CurrentUser, NewCredentials } from '../identity/identity-api';

export const PASSKEYS_URL = '/api/me/passkeys';
export const PASSWORD_URL = '/api/me/password';
export const PASSKEY_NAME_MAX_LENGTH = 50;

/** One of the signed-in user's passkeys. `id` is the credential id, base64url. */
export interface Passkey {
  id: string;
  name: string | null;
  createdAt: string;
  lastUsedAt: string | null;
}

/** Calls of My account (`/api/me`): the signed-in user's preferences, passkeys, password and sessions. Reads use `httpResource`. */
@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);

  changeUiLanguage(uiLanguage: UiLanguage): Promise<void> {
    return firstValueFrom(this.http.put<void>('/api/me/ui-language', { uiLanguage }));
  }

  changeTheme(theme: ThemePreference): Promise<void> {
    return firstValueFrom(this.http.put<void>('/api/me/theme', { theme }));
  }

  /** Ends the sessions on every other device; this one stays signed in. */
  signOutOtherSessions(): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/me/sign-out-others', null));
  }

  passkeyOptions(): Promise<PublicKeyCredentialCreationOptionsJSON> {
    return firstValueFrom(this.http.post<PublicKeyCredentialCreationOptionsJSON>(`${PASSKEYS_URL}/options`, null));
  }

  addPasskey(credential: PublicKeyCredentialJSON): Promise<Passkey> {
    return firstValueFrom(this.http.post<Passkey>(PASSKEYS_URL, { credential }));
  }

  renamePasskey(id: string, name: string): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${PASSKEYS_URL}/${encodeURIComponent(id)}/name`, { name }));
  }

  /** Removes the passkey and signs out every other device. The last sign-in method cannot be removed (409). */
  removePasskey(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${PASSKEYS_URL}/${encodeURIComponent(id)}`));
  }

  /**
   * Sets a password, with a username when the user has none, and signs out every other device. A taken username fails
   * with 400 and `errors.UserName`.
   */
  setPassword(credentials: NewCredentials): Promise<CurrentUser> {
    return firstValueFrom(this.http.post<CurrentUser>(PASSWORD_URL, credentials));
  }

  /** Changes the password and signs out every other device. A wrong current password fails with 400 and `errors.CurrentPassword`. */
  changePassword(currentPassword: string, newPassword: string): Promise<CurrentUser> {
    return firstValueFrom(this.http.put<CurrentUser>(PASSWORD_URL, { currentPassword, newPassword }));
  }

  /** Removes the password and signs out every other device. The last sign-in method cannot be removed (409). */
  removePassword(): Promise<CurrentUser> {
    return firstValueFrom(this.http.delete<CurrentUser>(PASSWORD_URL));
  }
}
