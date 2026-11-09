import { Injectable } from '@angular/core';

/** The person closed the passkey window, or it timed out. */
export class PasskeyCancelledError extends Error {}

/** The browser cannot use passkeys here (no WebAuthn JSON API, or not a secure context). */
export class PasskeyUnsupportedError extends Error {}

export interface PasskeyGetOptions {
  /** Offer the passkeys in the browser's autofill instead of a modal window. */
  conditional?: boolean;
  signal?: AbortSignal;
}

/** The browser side of the WebAuthn ceremony. */
@Injectable({ providedIn: 'root' })
export class Passkeys {
  isSupported(): boolean {
    return (
      typeof window !== 'undefined' &&
      window.isSecureContext &&
      typeof PublicKeyCredential !== 'undefined' &&
      typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
      typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function' &&
      !!navigator.credentials
    );
  }

  /** Whether the browser can offer passkeys in a field's autofill (conditional mediation). */
  async isAutofillSupported(): Promise<boolean> {
    if (!this.isSupported() || typeof PublicKeyCredential.isConditionalMediationAvailable !== 'function') {
      return false;
    }
    try {
      return await PublicKeyCredential.isConditionalMediationAvailable();
    } catch {
      return false;
    }
  }

  create(options: PublicKeyCredentialCreationOptionsJSON): Promise<PublicKeyCredentialJSON> {
    return this.ceremony(() =>
      navigator.credentials.create({ publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options) }),
    );
  }

  get(options: PublicKeyCredentialRequestOptionsJSON, { conditional = false, signal }: PasskeyGetOptions = {}): Promise<PublicKeyCredentialJSON> {
    return this.ceremony(() =>
      navigator.credentials.get({
        publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options),
        mediation: conditional ? 'conditional' : 'optional',
        signal,
      }),
    );
  }

  private async ceremony(run: () => Promise<Credential | null>): Promise<PublicKeyCredentialJSON> {
    if (!this.isSupported()) {
      throw new PasskeyUnsupportedError();
    }
    try {
      const credential = await run();
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
