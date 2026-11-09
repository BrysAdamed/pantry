# 0017 Invites, recovery links and user administration

**Status:** Accepted, 2026-11-09. Builds on [0004](0004-passkeys.md) and [0014](0014-spa-authentication.md).

## Context
ADR 0004 says the admin brings people in with an invite link and needs a recovery path for a lost phone. Both are bearer links: whoever has the link gets an account or a passkey on an account. The admin also removes users and changes roles, and the last admin must stay. Sessions are cookies that the app only rechecks against the database from time to time (Identity's security stamp validation, default every 30 minutes).

## Decision
- **One model for both links.** An invite and a recovery link are one `PasskeyLink` entity in the `identity` schema with a kind (`invite` for a member, `recovery` for a user), valid 48 hours, single use. A new link revokes the open one for the same member or user; a partial unique index keeps at most one pending link per target.
- **Token.** 32 random bytes, base64url, put in the link path (`/link/{token}`). Only its SHA-256 hash is stored, so the token is shown once, to the admin who issued it, and a database read does not reveal usable links. The SPA builds the address from its own origin, so the API does not need to know its public hostname.
- **Redeeming** runs the passkey creation ceremony of ADR 0014 under `/api/passkey-links/redeem/{token}` (anonymous). An invite creates the user and links the member in one transaction (ADR 0015, `IHouseholdModule.LinkMemberToUserAsync`); a recovery link adds a passkey and leaves the other passkeys alone (they are managed in My account, PAN-24). Expired, used and revoked links answer `410` with the state; unknown tokens `404`.
- **Serialized writes.** Issuing, revoking, redeeming, removing users and changing roles take one Postgres advisory lock for the transaction, so "single use" and "the last admin cannot be removed or demoted" hold under concurrent requests.
- **Admin endpoints** (`/api/passkey-links`, `/api/users`) require the `admin` policy and answer `403` to other users.
- **Removing a user** deletes the user with their passkeys and recovery links and unlinks the member (the member stays). There is no soft delete: a removed person comes back with a new invite.
- **Own passkeys** (My account, PAN-24, `/api/me/passkeys`): a user lists, adds, renames and removes their passkeys. The last passkey cannot be removed (checked under the same advisory lock). Removing a passkey changes the user's security stamp, so the sessions on every other device end at their next check, because a session does not record which passkey started it; the current session is reissued with the new stamp. The same is available on its own as "sign out other devices" (`POST /api/me/sign-out-others`). When a passkey last signed someone in is kept in our own `last_used_at` column on Identity's `user_passkeys` table, which Identity's store does not touch.
- **Sessions are rechecked every minute** (`Identity:Session:ValidationInterval`). The check rebuilds the role claim and rejects sessions of removed users, so a demotion or removal applies within a minute instead of 30. Integration tests set it to zero.

## Consequences
- A leaked link is usable until it is used, revoked or 48 hours pass. Admins can see and revoke open links.
- One user lookup per session per minute; negligible for a household.
- Identity's security stamp validator signs out of the two-factor remember-me scheme when it rejects a session, so that cookie scheme is registered (never issued) even though the app has no two-factor sign-in.
