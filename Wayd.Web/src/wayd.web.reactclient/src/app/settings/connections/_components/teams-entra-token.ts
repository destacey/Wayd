import { OidcProviderInfo } from '@/src/services/wayd-api'
import { getOrCreateUserManager } from '@/src/components/contexts/auth/oidc-client-registry'

/**
 * Scope the signed-in client must request so Entra issues an access token whose audience
 * is the Teams connection app. On-behalf-of rejects the Wayd API sign-in token: that
 * token's audience is the API app, and the Teams app is the client presenting the assertion.
 */
export function teamsAssertionScope(teamsClientId: string): string {
  return `api://${teamsClientId.trim()}/access_as_user`
}

export const TEAMS_MISSING_ENTRA_TOKEN_MESSAGE =
  'Sign in with Microsoft Entra ID to verify or test this connection. Wayd needs your Entra session to request an access token for the Teams app.'

export type TeamsSubjectToken = { token: string } | { error: string }

/**
 * Access token for the Teams app registration, taken from the existing Entra session.
 * The sign-in access token is left in place; only a rotated refresh token is written back.
 */
export async function getOidcSubjectAccessToken(
  loginProvider: string | null | undefined,
  providers: OidcProviderInfo[] | undefined,
  teamsClientId: string,
): Promise<TeamsSubjectToken> {
  const clientId = teamsClientId.trim()
  if (!clientId) {
    return { error: 'Enter the Teams application (client) ID before verifying.' }
  }

  if (!loginProvider || !providers?.length) {
    return { error: TEAMS_MISSING_ENTRA_TOKEN_MESSAGE }
  }

  const provider = providers.find((p) => p.name === loginProvider)
  if (!provider) return { error: TEAMS_MISSING_ENTRA_TOKEN_MESSAGE }

  const manager = getOrCreateUserManager(provider)
  const signedIn = await manager.getUser()
  if (!signedIn?.refresh_token) {
    return { error: TEAMS_MISSING_ENTRA_TOKEN_MESSAGE }
  }

  const scope = teamsAssertionScope(clientId)
  try {
    // signinSilent forwards `scope` to a silent authorize redirect, but a refresh-token
    // redeem uses the stored sign-in scope unless `extraTokenParams.scope` overrides it.
    // Both are set so the assertion audience is the Teams app either way.
    const teamsUser = await manager.signinSilent({
      scope,
      extraTokenParams: { scope },
    })
    const token = teamsUser?.access_token
    if (!token) {
      return { error: teamsScopeError(scope, 'Entra did not return an access token.') }
    }

    signedIn.refresh_token = teamsUser.refresh_token ?? signedIn.refresh_token
    try {
      await manager.storeUser(signedIn)
    } catch {
      // The Teams token is still usable. The stored sign-in token may now be the
      // Teams-audience token until the next Entra sign-in.
    }

    return { token }
  } catch (error) {
    return { error: teamsScopeError(scope, readTokenError(error)) }
  }
}

function teamsScopeError(scope: string, detail: string): string {
  return (
    `Wayd could not request an Entra access token for the Teams app (scope ${scope}). ` +
    'On that app registration, expose an API with Application ID URI api://{client-id} and a delegated scope named access_as_user. ' +
    "Grant that scope to Wayd's sign-in client (the identity provider's client id, not the Wayd API app) and grant admin consent. " +
    `Entra reported: ${detail}`
  )
}

function readTokenError(error: unknown): string {
  if (error instanceof Error && error.message.trim().length > 0) return error.message
  return 'Unknown error'
}
