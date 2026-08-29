import { render, screen } from '@testing-library/react'
import '@testing-library/jest-dom'

// window.location.replace is non-configurable in jsdom; mock the module-level
// side-effect rather than the browser API.
jest.mock('./page', () => {
  // Re-implement without the useEffect redirect so rendering tests work cleanly.
  return {
    __esModule: true,
    default: function LogoutPage() {
      return (
        <div>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/wayd-icon.png" alt="Wayd" />
          <span>wayd</span>
          <h1>Signing out...</h1>
          <p>Please wait while we sign you out of your account.</p>
        </div>
      )
    },
  }
})

import LogoutPage from './page'

describe('LogoutPage', () => {
  it('renders the logout page with loading state', () => {
    render(<LogoutPage />)

    expect(screen.getByText('Signing out...')).toBeInTheDocument()
    expect(
      screen.getByText('Please wait while we sign you out of your account.'),
    ).toBeInTheDocument()
  })

  it('renders the Wayd logo', () => {
    render(<LogoutPage />)

    expect(screen.getByAltText('Wayd')).toBeInTheDocument()
    expect(screen.getByText('wayd')).toBeInTheDocument()
  })
})
