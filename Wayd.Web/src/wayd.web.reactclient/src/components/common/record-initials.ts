/**
 * Two-letter initials for a person avatar.
 *
 * Prefers the structured name fields; falls back to the first letters of a
 * display name when they are absent. Returns at most two characters so the
 * glyph never overflows its circle.
 */
export const personInitials = (
  firstName?: string | null,
  lastName?: string | null,
  displayName?: string | null,
): string => {
  const first = firstName?.trim()
  const last = lastName?.trim()

  if (first || last) {
    return `${first?.[0] ?? ''}${last?.[0] ?? ''}`.toUpperCase()
  }

  const words = displayName?.trim().split(/\s+/).filter(Boolean) ?? []
  if (words.length === 0) return ''

  // A single-word display name still needs a glyph — use its first letter
  // rather than rendering an empty circle.
  const letters =
    words.length === 1
      ? words[0].slice(0, 1)
      : `${words[0][0]}${words[words.length - 1][0]}`

  return letters.toUpperCase()
}
