import { OidcProviderInfo } from '@/src/services/wayd-api'
import { getOrCreateUserManager } from '@/src/components/contexts/auth/oidc-client-registry'

/**
 * Returns the Entra/OIDC access token stored by oidc-client-ts for the signed-in provider, if any.
 * Used for Teams connection verify/test on-behalf-of Graph calls.
 */
export async function getOidcSubjectAccessToken(
  loginProvider: string | null | undefined,
  providers: OidcProviderInfo[] | undefined,
): Promise<string | null> {
  if (!loginProvider || !providers?.length) return null

  const provider = providers.find((p) => p.name === loginProvider)
  if (!provider) return null

  const manager = getOrCreateUserManager(provider)
  let user = await manager.getUser()
  if (!user || user.expired) {
    try {
      user = await manager.signinSilent()
    } catch {
      return null
    }
  }

  const token = user?.access_token
  return typeof token === 'string' && token.length > 0 ? token : null
}

export const TEAMS_MISSING_ENTRA_TOKEN_MESSAGE =
  'Sign in with Microsoft Entra ID to verify or test this connection. Wayd needs your Entra access token to send the Teams test message as you.'
