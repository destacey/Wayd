// Mocks must precede the import of the component under test.

const mockReplace = jest.fn()
const mockStoreReturnUrl = jest.fn()
const mockTakeReturnUrl = jest.fn()
let mockPathname = '/'
let mockSignedIn = false

jest.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({ replace: mockReplace }),
}))

jest.mock('@/src/services/clients', () => ({
  isAuthActive: () => mockSignedIn,
}))

jest.mock('@/src/utils/return-url', () => ({
  storeReturnUrl: () => mockStoreReturnUrl(),
  takeReturnUrl: () => mockTakeReturnUrl(),
}))

// The shell and the providers each reach the API or the browser; the gate's
// job is only to choose between them.
jest.mock('./app-header', () => ({
  __esModule: true,
  default: () => <div>App header</div>,
}))
jest.mock('./menu/app-side-nav', () => ({
  __esModule: true,
  default: () => <nav>Side nav</nav>,
}))
jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({ currentTheme: 'light' }),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => children,
}))
jest.mock('@/src/components/contexts/menu-toggle', () => ({
  MenuToggleProvider: ({ children }: { children: React.ReactNode }) => children,
}))
jest.mock('@/src/components/contexts/auth', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => children,
}))
jest.mock('@/src/components/contexts/messaging', () => ({
  MessageProvider: ({ children }: { children: React.ReactNode }) => children,
}))
jest.mock('../login/page', () => ({
  __esModule: true,
  default: () => <div>Login page</div>,
}))
jest.mock('../logout/page', () => ({
  __esModule: true,
  default: () => <div>Logout page</div>,
}))
jest.mock('../setup/page', () => ({
  __esModule: true,
  default: () => <div>Setup page</div>,
}))

import React from 'react'
import { render, screen } from '@testing-library/react'
import AuthGate from './auth-gate'

const renderGate = () =>
  render(
    <AuthGate>
      <div>Page content</div>
    </AuthGate>,
  )

describe('AuthGate', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockTakeReturnUrl.mockReturnValue(null)
  })

  it('shows the login page in place and remembers the page for a signed-out visitor', () => {
    // Arrange
    mockSignedIn = false
    mockPathname = '/oauth/consent'

    // Act
    renderGate()

    // Assert
    expect(screen.getByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Page content')).not.toBeInTheDocument()
    expect(mockStoreReturnUrl).toHaveBeenCalled()
  })

  it('renders the consent page without the app shell for a signed-in user', () => {
    // Arrange
    mockSignedIn = true
    mockPathname = '/oauth/consent'

    // Act
    renderGate()

    // Assert
    expect(screen.getByText('Page content')).toBeInTheDocument()
    expect(screen.queryByText('App header')).not.toBeInTheDocument()
    expect(screen.queryByText('Side nav')).not.toBeInTheDocument()
  })

  it('drops a stored return URL on a shell-free page without navigating to it', () => {
    // Arrange
    mockSignedIn = true
    mockPathname = '/oauth/consent'
    mockTakeReturnUrl.mockReturnValue('/oauth/consent?request=old')

    // Act
    renderGate()

    // Assert
    expect(mockTakeReturnUrl).toHaveBeenCalled()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('renders any other page inside the app shell and returns to a stored page', () => {
    // Arrange
    mockSignedIn = true
    mockPathname = '/ppm/projects'
    mockTakeReturnUrl.mockReturnValue('/ppm/projects/12?tab=tasks')

    // Act
    renderGate()

    // Assert
    expect(screen.getByText('Page content')).toBeInTheDocument()
    expect(screen.getByText('App header')).toBeInTheDocument()
    expect(mockReplace).toHaveBeenCalledWith('/ppm/projects/12?tab=tasks')
  })
})
