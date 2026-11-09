export const USER_NAME_MIN_LENGTH = 3;
export const USER_NAME_MAX_LENGTH = 32;
export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;

const USER_NAME = /^[\p{L}0-9._-]{3,32}$/u;
const LIKE_INTERNAL_ID = /^[0-9a-f]{32}$/i;

/** The username as the API keeps it: trimmed, Unicode NFC. */
export function cleanUserName(value: string): string {
  return value.trim().normalize('NFC');
}

/** Mirrors the API (ADR 0018): 3 to 32 letters, digits, `.`, `_` or `-`, never like an internal ID. */
export function isValidUserName(value: string): boolean {
  const userName = cleanUserName(value);
  return USER_NAME.test(userName) && !LIKE_INTERNAL_ID.test(userName);
}

/** Length only, no character-type rules. */
export function isValidPassword(value: string): boolean {
  return value.length >= PASSWORD_MIN_LENGTH && value.length <= PASSWORD_MAX_LENGTH;
}
