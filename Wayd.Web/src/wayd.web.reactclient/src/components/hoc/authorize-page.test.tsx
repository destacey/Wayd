// authorizePage.test.tsx
import React from 'react'
import { render, screen } from '@testing-library/react'
import authorizePage from './authorize-page'

// Create a dummy page component to wrap.
const DummyPage: React.FC = () => <div>Dummy Page</div>
DummyPage.displayName = 'DummyPage'

// We also need to stub the NotAuthorized component that the HOC renders when the user isn't authorized.
vi.mock('../common/not-authorized', () => {
  return { default: () => <div>Not Authorized</div> }
})

// Mock the useAuth hook so we can control its return value.
const mockHasClaim = vi.fn()
vi.mock('../contexts/auth', () => ({
  default: () => ({
    hasClaim: mockHasClaim,
  }),
}))

describe('authorizePage HOC', () => {
  beforeEach(() => {
    // Reset the mock between tests.
    mockHasClaim.mockReset()
  })

  test('renders wrapped page when authorized', () => {
    // Simulate an authorized user.
    mockHasClaim.mockReturnValue(true)

    const AuthorizedPage = authorizePage(DummyPage, 'Permission', 'test-value')
    render(<AuthorizedPage />)

    // Expect the DummyPage to be rendered.
    expect(screen.getByText('Dummy Page')).toBeInTheDocument()
    // And ensure the fallback NotAuthorized component is not rendered.
    expect(screen.queryByText('Not Authorized')).toBeNull()
  })

  test('renders NotAuthorized when unauthorized', () => {
    // Simulate an unauthorized user.
    mockHasClaim.mockReturnValue(false)

    const AuthorizedPage = authorizePage(DummyPage, 'Permission', 'test-value')
    render(<AuthorizedPage />)

    // Expect the NotAuthorized fallback to be rendered.
    expect(screen.getByText('Not Authorized')).toBeInTheDocument()
    // And ensure the DummyPage is not rendered.
    expect(screen.queryByText('Dummy Page')).toBeNull()
  })

  test('sets displayName correctly', () => {
    const AuthorizedPage = authorizePage(DummyPage, 'Permission', 'test-value')
    expect(AuthorizedPage.displayName).toBe('authorizedPage(DummyPage)')
  })
})
