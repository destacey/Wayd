import { OidcProviderInfo } from '@/src/services/wayd-api'
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { navigateWithFullReload } from '@/src/utils/window-utils'

// One UserManager per provider name, built on first use. The redirect state
// that signinRedirectCallback() matches against lives in oidc-client-ts's
// localStorage state store, not on the instance, so any manager built from
// the same provider settings can complete the callback.
const managers = new Map<string, UserManager>()

// sessionStorage: survives the redirect roundtrip, cleared on completion.
const CHOSEN_PROVIDER_KEY = 'wayd.oidc.chosenProvider'

function buildUserManager(provider: OidcProviderInfo): UserManager {
  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  return new UserManager({
    authority: provider.authority,
    client_id: provider.clientId,
    redirect_uri: origin + '/login',
    post_logout_redirect_uri: origin + '/login',
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
 * the provider's user (whose access token is exchanged for a Wayd token).
 */
export async function completeSignin(provider: OidcProviderInfo): Promise<User> {
  const manager = getOrCreateUserManager(provider)
  return manager.signinRedirectCallback()
}

/**
 * Initiates end-session redirect for the given provider. Falls back to a
 * simple /login redirect if the provider has no end-session endpoint (some
 * generic OIDC providers omit it).
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
