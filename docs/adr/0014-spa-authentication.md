# 0014 SPA authentication: Identity passkeys over JSON, cookie session, antiforgery header

**Status:** Accepted, 2026-11-09. Amended by [0018](0018-password-sign-in.md): passwords are allowed next to passkeys, with password sign-in under `auth/*`.

## Context
ADR 0004 picks ASP.NET Core Identity with passkeys. The .NET 10 templates show passkeys in Blazor pages, but the app is an Angular SPA calling `/api`. We checked the .NET 10 API before building on it: `SignInManager.MakePasskeyCreationOptionsAsync`, `PerformPasskeyAttestationAsync`, `MakePasskeyRequestOptionsAsync` and `PasskeySignInAsync` take and return WebAuthn JSON and need no Razor or Blazor code, and users do not need a password. Between the options call and the attestation call, `SignInManager` keeps the ceremony state in the `Identity.TwoFactorUserId` cookie.

## Decision
- Identity is registered with `AddIdentityCore` + `AddSignInManager` + EF stores (schema version 3, passkey table) in the `identity` schema. No Identity UI, no passwords, no roles table: the role (`admin` / `user`) is a column on the user and becomes a role claim.
- The SPA runs the WebAuthn ceremony in the browser (`navigator.credentials.create/get`) and posts the JSON to vertical-slice endpoints that call `SignInManager`. No workaround over the built-in API is needed.
- Relying party ID and allowed origins come from configuration (`Identity:Passkeys`), validated on startup. Origin checks use `IdentityPasskeyOptions.ValidateOrigin` against that list, so the dev proxy (`ng serve` on another port) and a reverse proxy in production both work.
- Session: the Identity application cookie, HttpOnly, Secure, `SameSite=Strict`, persistent with sliding expiry (`Identity:Session:Lifetime`, default 30 days). `/api` answers 401/403 instead of redirecting.
- Every `/api` endpoint requires a signed-in user unless it opts out with `AllowAnonymous`: `health`, `antiforgery`, `setup/*`, `auth/*` (passkey options, sign-in, sign-out) and `passkey-links/redeem/*` (ADR 0017).
- Antiforgery: every unsafe `/api` request must carry the `X-XSRF-TOKEN` header. The API sends the token in a readable `XSRF-TOKEN` cookie on safe `/api` requests (GET, HEAD, OPTIONS, TRACE) and after sign-in and sign-out; Angular's `HttpClient` echoes it automatically. Tokens are bound to the user, so:
  - tokens are issued before authorization runs, so even a 401 answer carries a fresh token for the now anonymous browser;
  - tokens are validated after authorization, so an unsafe request whose session expired gets 401 (sign in again) rather than 400;
  - `GET /api/antiforgery` (anonymous, 204) is the explicit token source; the sign-in screen calls it before starting the ceremony, because a token left over from an expired session would make the sign-in POST fail.
- Sign-in (PAN-20): `POST /api/auth/passkey-options` (`MakePasskeyRequestOptionsAsync(null)`, discoverable credentials, no user name) and `POST /api/auth/sign-in`. The sign-in slice runs `PerformPasskeyAssertionAsync` and the same checks as `PasskeySignInAsync` itself (can sign in, not locked out, update the passkey), because `PasskeySignInAsync` always creates a non-persistent session and does not return the user. A failed sign-in answers 401. `POST /api/auth/sign-out` is anonymous and idempotent.
- Only the two cookie schemes in use are registered: the application cookie (`pantry.session`, security stamp validation) and the passkey ceremony cookie (`pantry.passkey`, 5 minutes). Identity's external-login cookie is not registered. The two-factor remember-me scheme is registered but never issued, because the security stamp validator signs out of it when it rejects a session (ADR 0017). `SignInManager` clears the ceremony cookie when it reads the ceremony state.
- `/api` responses are `Cache-Control: no-store`.
- First run is guarded by a Postgres advisory lock taken in the same transaction that checks for users and creates the admin, so two concurrent first runs create one admin.

## Consequences
- Passkey sign-in (PAN-20) and adding a passkey from an invite (PAN-23) reuse the same pattern.
- The session cookie is always `Secure`, so it needs HTTPS or `localhost`; integration tests use an `https://localhost` base address. The antiforgery cookies follow the request scheme, because the app speaks plain HTTP behind the TLS proxy (ADR 0006).
- Forwarded headers (so the app sees the proxy's `https` scheme) are configured in E10 together with the hostname.
- The SPA must call one `/api` GET (for example `/api/setup` or `/api/me`) before its first POST, which it does on start.
