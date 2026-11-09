import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PasskeyLinkKind } from './identity-api';

export const PASSKEY_LINKS_URL = '/api/passkey-links';
export const REDEEM_PATH = 'link';

/** An invite or recovery link that can still be used. */
export interface OpenPasskeyLink {
  id: string;
  kind: PasskeyLinkKind;
  memberId: string | null;
  userId: string | null;
  createdAt: string;
  expiresAt: string;
}

/** A link right after it was issued: the only time its token is known. */
export interface IssuedPasskeyLink {
  id: string;
  kind: PasskeyLinkKind;
  token: string;
  memberId: string | null;
  userId: string | null;
  expiresAt: string;
}

/** Admin-only calls of the Identity module: invites, recovery links, removing users and changing roles. */
@Injectable({ providedIn: 'root' })
export class UserAdminApi {
  private readonly http = inject(HttpClient);

  issueInvite(memberId: string): Promise<IssuedPasskeyLink> {
    return firstValueFrom(this.http.post<IssuedPasskeyLink>(`${PASSKEY_LINKS_URL}/invites`, { memberId }));
  }

  issueRecoveryLink(userId: string): Promise<IssuedPasskeyLink> {
    return firstValueFrom(this.http.post<IssuedPasskeyLink>(`${PASSKEY_LINKS_URL}/recovery`, { userId }));
  }

  revokeLink(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${PASSKEY_LINKS_URL}/${id}`));
  }

  removeUser(userId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/users/${userId}`));
  }

  changeRole(userId: string, role: 'admin' | 'user'): Promise<void> {
    return firstValueFrom(this.http.put<void>(`/api/users/${userId}/role`, { role }));
  }
}

/** The address to send: the redeem screen of this app with the token. */
export function passkeyLinkUrl(token: string, origin = window.location.origin): string {
  return `${origin}/${REDEEM_PATH}/${encodeURIComponent(token)}`;
}
