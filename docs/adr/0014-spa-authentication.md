# 0014 SPA authentication: Identity passkeys over JSON, cookie session, antiforgery header

**Status:** Accepted, 2026-11-09

## Context
ADR 0004 picks ASP.NET Core Identity with passkeys. The .NET 10 templates show passkeys in Blazor pages, but the app is an Angular SPA calling `/api`. We checked the .NET 10 API before building on it: `SignInManager.MakePasskeyCreationOptionsAsync`, `PerformPasskeyAttestationAsync`, `MakePasskeyRequestOptionsAsync` and `PasskeySignInAsync` take and return WebAuthn JSON and need no Razor or Blazor code, and users do not need a password. Between the options call and the attestation call, `SignInManager` keeps the ceremony state in the `Identity.TwoFactorUserId` cookie.

## Decision
- Identity is registered with `AddIdentityCore` + `AddSignInManager` + EF stores (schema version 3, passkey table) in the `identity` schema. No Identity UI, no passwords, no roles table: the role (`admin` / `user`) is a column on the user and becomes a role claim.
- The SPA runs the WebAuthn ceremony in the browser (`navigator.credentials.create/get`) and posts the JSON to vertical-slice endpoints that call `SignInManager`. No workaround over the built-in API is needed.
- Relying party ID and allowed origins come from configuration (`Identity:Passkeys`), validated on startup. Origin checks use `IdentityPasskeyOptions.ValidateOrigin` against that list, so the dev proxy (`ng serve` on another port) and a reverse proxy in production both work.
- Session: the Identity application cookie, HttpOnly, Secure, `SameSite=Strict`, persistent with sliding expiry (`Identity:Session:Lifetime`, default 30 days). `/api` answers 401/403 instead of redirecting.
- Every `/api` endpoint requires a signed-in user unless it opts out with `AllowAnonymous` (health, setup, sign-in).
- Antiforgery: every unsafe `/api` request must carry the `X-XSRF-TOKEN` header. The API sends the token in a readable `XSRF-TOKEN` cookie on `/api` GETs and after sign-in; Angular's `HttpClient` echoes it automatically.
- First run is guarded by a Postgres advisory lock taken in the same transaction that checks for users and creates the admin, so two concurrent first runs create one admin.

## Consequences
- Passkey sign-in (PAN-20) and adding a passkey from an invite (PAN-23) reuse the same pattern.
- The session cookie is always `Secure`, so it needs HTTPS or `localhost`; integration tests use an `https://localhost` base address. The antiforgery cookies follow the request scheme, because the app speaks plain HTTP behind the TLS proxy (ADR 0006).
- Forwarded headers (so the app sees the proxy's `https` scheme) are configured in E10 together with the hostname.
- The SPA must call one `/api` GET (for example `/api/setup` or `/api/me`) before its first POST, which it does on start.
