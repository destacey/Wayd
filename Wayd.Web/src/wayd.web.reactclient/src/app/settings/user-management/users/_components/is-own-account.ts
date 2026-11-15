/**
 * Whether the account is the signed-in user's own. User administration refuses changes to your own roles and
 * employee link, so the forms hide or disable them here rather than let the request fail.
 *
 * Compared by name because an identity provider's session carries its token, not Wayd's user id. The session's
 * username comes from the token's email or preferred username, so it is matched against both the account's
 * user name and its email, ignoring case.
 */
export function isOwnAccount(
  account:
    { userName?: string | null; email?: string | null } | null | undefined,
  signedInUsername: string | null | undefined,
): boolean {
  if (!account || !signedInUsername) return false
  const signedIn = signedInUsername.toLowerCase()
  return [account.userName, account.email].some(
    (name) => !!name && name.toLowerCase() === signedIn,
  )
}
