import { OidcProviderInfo } from '@/src/services/wayd-api'
import {
  InMemoryWebStorage,
  User,
  UserManager,
  WebStorageStateStore,
} from 'oidc-client-ts'
import { navigateWithFullReload } from '@/src/utils/window-utils'

// One UserManager per provider name, built on first use. The redirect state
// that signinRedirectCallback() matches against lives in oidc-client-ts's
// localStorage state store, not on the instance, so any manager built from
// the same provider settings can complete the callback.
const managers = new Map<string, UserManager>()

// sessionStorage: survives the redirect roundtrip, cleared on completion.
const CHOSEN_PROVIDER_KEY = 'wayd.oidc.chosenProvider'

// The /login query parameter naming the provider whose session to end.
const SIGNOUT_PARAM = 'signout'

function buildUserManager(provider: OidcProviderInfo): UserManager {
  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  return new UserManager({
    authority: provider.authority,
    client_id: provider.clientId,
    redirect_uri: origin + '/login',
    post_logout_redirect_uri: origin + '/login',
    scope: provider.scopes.join(' '),
    response_type: 'code',
    // The code comes back in the fragment, which the browser never sends to a server, so it stays
    // out of the client's request logs and traces.
    response_mode: 'fragment',
    // The provider's tokens are needed only for the exchange that follows the redirect, so they are
    // held in memory and gone on the next navigation; nothing of them is written to storage.
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    automaticSilentRenew: false,
  })
}

export function getOrCreateUserManager(provider: OidcProviderInfo): UserManager {
  const existing = managers.get(provider.name)
  if (existing) return existing
  const manager = buildUserManager(provider)
  managers.set(provider.name, manager)
  return manager
}

// --- Redirect-roundtrip provider tracking (sessionStorage) ---

export function setChosenProvider(providerName: string): void {
  sessionStorage.setItem(CHOSEN_PROVIDER_KEY, providerName)
}

export function getChosenProviderName(): string | null {
  return sessionStorage.getItem(CHOSEN_PROVIDER_KEY)
}

export function clearChosenProvider(): void {
  sessionStorage.removeItem(CHOSEN_PROVIDER_KEY)
}

// --- Provider sign-out handoff ---

/**
 * The login page URL that ends the user's session with `providerName`. Sign-out
 * runs there because only that page's Content-Security-Policy lets the browser
 * reach a provider.
 */
export function providerSignoutPath(providerName: string): string {
  return `/login?${SIGNOUT_PARAM}=${encodeURIComponent(providerName)}`
}

/** The provider the current /login URL asks to sign out of, or null. */
export function readProviderSignout(): string | null {
  if (typeof window === 'undefined') return null
  return new URL(window.location.href).searchParams.get(SIGNOUT_PARAM)
}

// --- Auth flows ---

/**
 * Starts the PKCE redirect flow for the given provider. Stores which provider
 * was chosen (sessionStorage) so that `completeSignin` can match after the
 * redirect.
 */
export async function signinRedirect(provider: OidcProviderInfo): Promise<void> {
  setChosenProvider(provider.name)
  const manager = getOrCreateUserManager(provider)
  await manager.signinRedirect()
}

/**
 * Completes the redirect callback for the provider the user chose, returning
 * the provider's user (whose access token is exchanged for a Wayd token). The
 * manager keeps no copy of it.
 */
export async function completeSignin(provider: OidcProviderInfo): Promise<User> {
  const manager = getOrCreateUserManager(provider)
  const user = await manager.signinRedirectCallback()
  await manager.removeUser()
  return user
}

/**
 * Initiates end-session redirect for the given provider. No ID token is kept,
 * so the request names the client (`client_id`) rather than the session
 * (`id_token_hint`); a provider may ask the user to confirm. Falls back to a
 * simple /login redirect if the provider has no end-session endpoint (some
 * generic OIDC providers omit it).
 *
 * Call it only from /login: it fetches the provider's discovery document, and
 * only that page's Content-Security-Policy lets the browser reach a provider.
 */
export async function signoutRedirect(provider: OidcProviderInfo): Promise<void> {
  const manager = getOrCreateUserManager(provider)
  try {
    await manager.signoutRedirect()
  } catch {
    // Provider has no end_session_endpoint — just go to login.
    navigateWithFullReload('/login')
  }
}
