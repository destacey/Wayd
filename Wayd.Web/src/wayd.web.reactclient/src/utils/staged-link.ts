import { instantTime, type Instant } from './instant'

/**
 * Whether a staged identity link — a tenant or provider migration, or an
 * admin-created user's first sign-in — can still be completed by the user's
 * sign-in. One with no expiry was staged before its time was recorded, and the
 * API treats it as expired; at the moment of expiry it is still open, as the
 * API treats it.
 */
export const isStagedLinkOpen = (
  expiresAt: Instant | null | undefined,
  now: number,
): boolean => !!expiresAt && instantTime(expiresAt) >= now
