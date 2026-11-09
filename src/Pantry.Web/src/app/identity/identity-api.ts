import { HttpClient } from '@angular/common/http';
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

/** Calls of the Identity module (`/api/setup`, `/api/me`). */
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
}
