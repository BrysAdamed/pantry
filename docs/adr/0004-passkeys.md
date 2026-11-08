# 0004 ASP.NET Identity with passkeys

**Status:** Accepted, 2026-11-08

## Context
Two users on Android and iPhone. No identity provider exists in the homelab. Passwords are a chore on phones.

## Decision
- ASP.NET Core Identity with passkeys (WebAuthn), built into .NET 10.
- First run: the app asks to create the admin account.
- The admin invites further users (invite link) or creates them.
- Cookie auth for the SPA. Personal access tokens for MCP clients (see 0005).
- An external OIDC provider can be added later without changing the user model.

## Consequences
- No passwords to manage. Passkeys sync through iCloud Keychain and Google Password Manager.
- Passkeys require a secure context and a stable hostname (the relying party ID). Works on `localhost` for development; production needs HTTPS on a real hostname (see 0006).
- A recovery path is needed (e.g. admin can issue a new invite for an existing user).
