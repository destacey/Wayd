import { OidcProviderInfo } from '@/src/services/wayd-api'
import { getOrCreateUserManager } from '@/src/components/contexts/auth/oidc-client-registry'
import { getOidcSubjectAccessToken } from './teams-entra-token'

jest.mock('@/src/components/contexts/auth/oidc-client-registry', () => ({
  getOrCreateUserManager: jest.fn(),
}))

const provider: OidcProviderInfo = {
  name: 'MicrosoftEntraId',
  displayName: 'Microsoft Entra ID',
  providerType: 'MicrosoftEntraId',
  authority: 'https://login.microsoftonline.com/tenant/v2.0',
  clientId: '8025424b-e1f0-43b2-9702-c386fbf76baf',
  scopes: [
    'openid',
    'profile',
    'email',
    'api://cc9e121a-bdb0-487d-bc7d-e2744a3dd343/access_as_user',
  ],
}

const teamsClientId = 'ccbef405-198d-4cf5-883d-8c4651276a7f'
const teamsScope = `api://${teamsClientId}/access_as_user`

function mockManager(overrides?: {
  user?: Record<string, unknown> | null
  silent?: jest.Mock
}) {
  const user = overrides && 'user' in overrides
    ? overrides.user
    : {
        access_token: 'sign-in-api-token',
        refresh_token: 'refresh-1',
        scope: provider.scopes.join(' '),
        expires_at: 9_999_999_999,
        token_type: 'Bearer',
        session_state: null,
        profile: {},
      }
  const signinSilent =
    overrides?.silent ??
    jest.fn().mockResolvedValue({
      access_token: 'teams-audience-token',
      refresh_token: 'refresh-2',
    })
  const storeUser = jest.fn().mockResolvedValue(undefined)
  const manager = {
    getUser: jest.fn().mockResolvedValue(user),
    signinSilent,
    storeUser,
  }
  jest.mocked(getOrCreateUserManager).mockReturnValue(manager as never)
  return manager
}

describe('getOidcSubjectAccessToken', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('requests a token whose audience is the Teams app, not the sign-in API', async () => {
    const manager = mockManager()

    const result = await getOidcSubjectAccessToken(
      provider.name,
      [provider],
      `  ${teamsClientId}  `,
    )

    expect(result).toEqual({ token: 'teams-audience-token' })
    expect(manager.signinSilent).toHaveBeenCalledWith({
      scope: teamsScope,
      extraTokenParams: { scope: teamsScope },
    })
    expect(manager.storeUser).toHaveBeenCalledWith(
      expect.objectContaining({
        access_token: 'sign-in-api-token',
        refresh_token: 'refresh-2',
      }),
    )
  })

  it('reports the Entra error when the Teams scope cannot be issued', async () => {
    mockManager({
      silent: jest.fn().mockRejectedValue(new Error('AADSTS65001: consent required')),
    })

    const result = await getOidcSubjectAccessToken(
      provider.name,
      [provider],
      teamsClientId,
    )

    expect(result).toEqual({
      error: expect.stringContaining('AADSTS65001: consent required'),
    })
    expect(result).toEqual({
      error: expect.stringContaining(teamsScope),
    })
  })

  it('asks for an Entra sign-in when the browser has no session', async () => {
    mockManager({ user: null })

    const result = await getOidcSubjectAccessToken(
      provider.name,
      [provider],
      teamsClientId,
    )

    expect(result).toEqual({
      error: expect.stringContaining('Sign in with Microsoft Entra ID'),
    })
  })
})
