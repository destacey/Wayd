// Mocks must precede the import of the component under test.

const mockReplace = vi.fn()
const mockStoreReturnUrl = vi.fn()
const mockTakeReturnUrl = vi.fn()
let mockPathname = '/'
let mockSignedIn = false

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({ replace: mockReplace }),
}))

vi.mock('@/src/services/clients', () => ({
  isAuthActive: () => mockSignedIn,
}))

vi.mock('@/src/utils/return-url', () => ({
  storeReturnUrl: () => mockStoreReturnUrl(),
  takeReturnUrl: () => mockTakeReturnUrl(),
}))

// The shell and the providers each reach the API or the browser; the gate's
// job is only to choose between them.
vi.mock('./app-header', () => ({
  default: () => <div>App header</div>,
}))
vi.mock('./menu/app-side-nav', () => ({
  default: () => <nav>Side nav</nav>,
}))
vi.mock('@/src/components/contexts/theme', () => ({
  default: () => ({ currentTheme: 'light' }),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => children,
}))
vi.mock('@/src/components/contexts/menu-toggle', () => ({
  MenuToggleProvider: ({ children }: { children: React.ReactNode }) => children,
}))
vi.mock('@/src/components/contexts/auth', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => children,
}))
vi.mock('@/src/components/contexts/messaging', () => ({
  MessageProvider: ({ children }: { children: React.ReactNode }) => children,
}))
vi.mock('../login/page', () => ({
  default: () => <div>Login page</div>,
}))
vi.mock('../logout/page', () => ({
  default: () => <div>Logout page</div>,
}))
vi.mock('../setup/page', () => ({
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
    vi.clearAllMocks()
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
