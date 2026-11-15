// @vitest-environment jsdom
import { clearAuth } from './auth-storage'

describe('clearAuth', () => {
  it('removes the identity provider redirect state from both storages', () => {
    // Arrange
    localStorage.setItem('oidc.abandoned-state', '{"code_verifier":"v"}')
    sessionStorage.setItem('oidc.user:https://idp.example:client', '{}')
    localStorage.setItem('wayd.theme', 'dark')

    // Act
    clearAuth()

    // Assert
    expect(localStorage.getItem('oidc.abandoned-state')).toBeNull()
    expect(
      sessionStorage.getItem('oidc.user:https://idp.example:client'),
    ).toBeNull()
    expect(localStorage.getItem('wayd.theme')).toBe('dark')
  })
})
