import { OidcProviderInfo } from '@/src/services/wayd-api'
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { navigateWithFullReload } from '@/src/utils/window-utils'

// Module-level cache: one UserManager per provider name. Persisting across
// renders is required because signinRedirectCallback() must be called on the
// same instance that initiated the redirect, otherwise oidc-client-ts can't
// match the state parameter and throws.
const managers = new Map<string, UserManager>()

// sessionStorage: survives the redirect roundtrip, cleared on completion.
const CHOSEN_PROVIDER_KEY = 'wayd.oidc.chosenProvider'

// localStorage: persists across sessions so silent sign-in can try the last
// provider automatically on the next visit.
const LAST_PROVIDER_KEY = 'wayd.oidc.lastProvider'

function buildUserManager(provider: OidcProviderInfo): UserManager {
  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  return new UserManager({
    authority: provider.authority,
    client_id: provider.clientId,
    redirect_uri: origin + '/login',
    post_logout_redirect_uri: origin + '/login',
    // Silent signin uses a hidden iframe; it needs its own redirect target so
    // the iframe callback doesn't trigger the full redirect handler.
    silent_redirect_uri: origin + '/login?silent=1',
    scope: provider.scopes.join(' '),
    response_type: 'code',
    userStore: new WebStorageStateStore({ store: localStorage }),
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

// --- Last-used provider tracking (localStorage) ---

export function setLastProvider(providerName: string): void {
  localStorage.setItem(LAST_PROVIDER_KEY, providerName)
}

export function getLastProviderName(): string | null {
  return localStorage.getItem(LAST_PROVIDER_KEY)
}

export function clearLastProvider(): void {
  localStorage.removeItem(LAST_PROVIDER_KEY)
}

// --- Auth flows ---

/**
 * Starts the PKCE redirect flow for the given provider. Stores which provider
 * was chosen (sessionStorage) and which was last used (localStorage) so that
 * `completeSignin` can match after the redirect and future visits can attempt
 * silent sign-in automatically.
 */
export async function signinRedirect(provider: OidcProviderInfo): Promise<void> {
  setChosenProvider(provider.name)
  const manager = getOrCreateUserManager(provider)
  await manager.signinRedirect()
}

/**
 * Completes the redirect callback. Must be called on the same UserManager
 * instance that started the redirect — that's why the manager map is
 * module-level. Records the provider as last-used on success.
 */
export async function completeSignin(provider: OidcProviderInfo): Promise<User> {
  const manager = getOrCreateUserManager(provider)
  const user = await manager.signinRedirectCallback()
  setLastProvider(provider.name)
  return user
}

/**
 * Attempts a silent sign-in via hidden iframe for the given provider.
 * Returns the OIDC User on success, or null if the IdP has no active session
 * (login_required, interaction_required, or any network/timeout error).
 *
 * Callers should treat null as "show the login button" rather than an error.
 */
export async function signinSilent(provider: OidcProviderInfo): Promise<User | null> {
  try {
    const manager = getOrCreateUserManager(provider)
    const user = await manager.signinSilent()
    if (user) {
      setLastProvider(provider.name)
    }
    return user ?? null
  } catch {
    // login_required, interaction_required, timeout, no SSO cookie — all
    // expected; caller falls back to showing the login button.
    return null
  }
}

/**
 * Initiates end-session redirect for the given provider. Falls back to a
 * simple /login redirect if the provider has no end-session endpoint (some
 * generic OIDC providers omit it). Clears the last-used provider record so
 * the next visit doesn't attempt silent sign-in for the signed-out session.
 */
export async function signoutRedirect(provider: OidcProviderInfo): Promise<void> {
  clearLastProvider()
  const manager = getOrCreateUserManager(provider)
  try {
    await manager.signoutRedirect()
  } catch {
    // Provider has no end_session_endpoint — just go to login.
    navigateWithFullReload('/login')
  }
}
