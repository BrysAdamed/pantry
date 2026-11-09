import { Injectable } from '@angular/core';

/** The person closed the passkey window, or it timed out. */
export class PasskeyCancelledError extends Error {}

/** The browser cannot create passkeys here (no WebAuthn JSON API, or not a secure context). */
export class PasskeyUnsupportedError extends Error {}

/** The browser side of the WebAuthn ceremony. */
@Injectable({ providedIn: 'root' })
export class Passkeys {
  isSupported(): boolean {
    return (
      typeof window !== 'undefined' &&
      window.isSecureContext &&
      typeof PublicKeyCredential !== 'undefined' &&
      typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
      !!navigator.credentials
    );
  }

  async create(options: PublicKeyCredentialCreationOptionsJSON): Promise<PublicKeyCredentialJSON> {
    if (!this.isSupported()) {
      throw new PasskeyUnsupportedError();
    }
    try {
      const credential = await navigator.credentials.create({
        publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options),
      });
      if (!(credential instanceof PublicKeyCredential)) {
        throw new PasskeyCancelledError();
      }
      return credential.toJSON();
    } catch (error) {
      if (error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')) {
        throw new PasskeyCancelledError();
      }
      throw error;
    }
  }
}
