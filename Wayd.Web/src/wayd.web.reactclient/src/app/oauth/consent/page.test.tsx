// Mocks must precede the import of the component under test.

const mockGet = jest.fn()
const mockApprove = jest.fn()
const mockDeny = jest.fn()
const mockNavigate = jest.fn()
let mockFlag = { isEnabled: true, isLoading: false }

jest.mock('@/src/services/clients', () => ({
  getOAuthAuthorizationRequestsClient: () => ({
    get: mockGet,
    approve: mockApprove,
    deny: mockDeny,
  }),
}))

jest.mock('@/src/hooks', () => ({
  useDocumentTitle: jest.fn(),
  useFeatureFlag: () => mockFlag,
}))

let mockFramed = false

jest.mock('@/src/utils/window-utils', () => ({
  navigateWithFullReload: (url: string) => mockNavigate(url),
  isFramed: () => mockFramed,
}))

import { act, fireEvent, render, screen } from '@testing-library/react'
import { useSearchParams } from 'next/navigation'
import ConsentPage from './page'
import { APPROVE_DELAY_MS } from './use-approve-armed'

const HANDLE = 'handle-abc123'

const pendingRequest = (overrides: Record<string, unknown> = {}) => ({
  clientId: 'https://tools.acme.example/client.json',
  clientHost: 'tools.acme.example',
  clientName: 'Acme Assistant',
  redirectUri: 'https://tools.acme.example/callback',
  redirectHost: 'tools.acme.example',
  redirectHostDiffers: false,
  redirectIsLoopback: false,
  expiresAt: new Date('2026-10-20T15:10:00Z'),
  ...overrides,
})

// The generated client throws the parsed problem details for a documented status.
const problem = (status: number) => ({ status, title: 'Problem' })
// Axios rejects with no response when the server cannot be reached.
const networkError = () =>
  Object.assign(new Error('Network Error'), { code: 'ERR_NETWORK' })

const withRequestParam = (handle: string | null) => {
  jest
    .mocked(useSearchParams)
    .mockReturnValue(
      new URLSearchParams(handle ? { request: handle } : {}) as never,
    )
}

// The page loads and decides through plain promises, whose state updates reach
// the DOM only inside an async act.
const renderPage = async () => {
  await act(async () => {
    render(<ConsentPage />)
  })
}

const click = async (name: string) => {
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name }))
  })
}

const advance = async (ms: number) => {
  await act(async () => {
    jest.advanceTimersByTime(ms)
  })
}

// Approve stays disabled for a moment after the request shows; most tests start past that.
const renderReady = async (overrides: Record<string, unknown> = {}) => {
  mockGet.mockResolvedValue(pendingRequest(overrides))
  await renderPage()
  await advance(APPROVE_DELAY_MS)
}

const setVisibility = (state: DocumentVisibilityState) => {
  Object.defineProperty(document, 'visibilityState', {
    configurable: true,
    get: () => state,
  })
}

describe('ConsentPage', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockFlag = { isEnabled: true, isLoading: false }
    mockFramed = false
    withRequestParam(HANDLE)
    setVisibility('visible')
    jest.useFakeTimers()
  })

  afterEach(() => {
    jest.useRealTimers()
  })

  describe('showing the request', () => {
    it('reads the request by the handle in the query string', async () => {
      // Act
      await renderReady()

      // Assert
      expect(mockGet).toHaveBeenCalledWith(HANDLE)
    })

    it('shows the client host as the heading and the name as its claim', async () => {
      // Act
      await renderReady()

      // Assert
      expect(
        screen.getByRole('heading', { name: 'tools.acme.example' }),
      ).toBeInTheDocument()
      expect(
        screen.getByText('It calls itself “Acme Assistant”.'),
      ).toBeInTheDocument()
      expect(
        screen.queryByRole('heading', { name: /Acme Assistant/ }),
      ).not.toBeInTheDocument()
    })

    it('renders the client name as text, never as markup', async () => {
      // Arrange
      const name = '<img src="x" onerror="alert(1)">'

      // Act
      await renderReady({ clientName: name })

      // Assert
      expect(screen.getByText(`It calls itself “${name}”.`)).toBeInTheDocument()
      expect(document.querySelector('img[src="x"]')).toBeNull()
    })

    it('says what approving grants and where to revoke it', async () => {
      // Act
      await renderReady()

      // Assert
      expect(
        screen.getByText(/as you, through Wayd's MCP endpoint/),
      ).toBeInTheDocument()
      expect(screen.getByText(/all of your permissions/)).toBeInTheDocument()
      expect(screen.getByText(/Account → Sessions/)).toBeInTheDocument()
    })
  })

  describe('where the user goes next', () => {
    it('warns when the redirect host differs from the client host', async () => {
      // Act
      await renderReady({
        redirectHost: 'collector.other.example',
        redirectHostDiffers: true,
      })

      // Assert
      expect(
        screen.getByText('This sends you to a different site'),
      ).toBeInTheDocument()
      expect(
        screen.getByText(
          /at tools\.acme\.example, but you will be sent to collector\.other\.example/,
        ),
      ).toBeInTheDocument()
    })

    it('shows no warning when the redirect stays on the client host', async () => {
      // Act
      await renderReady()

      // Assert
      expect(
        screen.queryByText('This sends you to a different site'),
      ).not.toBeInTheDocument()
      expect(screen.getByText(/you will be sent to/)).toHaveTextContent(
        'tools.acme.example',
      )
    })

    it('describes a loopback redirect as an app on this computer, without a warning', async () => {
      // Act
      await renderReady({
        redirectUri: 'http://127.0.0.1:33418/callback',
        redirectHost: '127.0.0.1',
        redirectIsLoopback: true,
      })

      // Assert
      expect(
        screen.getByText(/return to an app on this computer/),
      ).toBeInTheDocument()
      expect(
        screen.queryByText('This sends you to a different site'),
      ).not.toBeInTheDocument()
    })

    it('describes an app-scheme redirect as opening an app', async () => {
      // Act
      await renderReady({
        redirectUri: 'cursor://anysphere.cursor-mcp/oauth/callback',
        redirectHost: null,
      })

      // Assert
      expect(screen.getByText(/your browser opens an app/)).toBeInTheDocument()
    })
  })

  describe('deciding', () => {
    it.each([
      [
        'Approve',
        mockApprove,
        'https://tools.acme.example/callback?code=c1&state=s1',
      ],
      [
        'Deny',
        mockDeny,
        'https://tools.acme.example/callback?error=access_denied&state=s1',
      ],
    ])(
      '%s calls the API and navigates to the redirect URL it answers',
      async (button, call, redirectUrl) => {
        // Arrange
        call.mockResolvedValue({ redirectUrl })
        await renderReady()

        // Act
        await click(button)

        // Assert
        screen.getByText(/Returning you to the application/)
        expect(call).toHaveBeenCalledWith(HANDLE)
        expect(mockNavigate).toHaveBeenCalledWith(redirectUrl)
      },
    )

    it('sends one call when Approve is clicked twice before the page re-renders', async () => {
      // Arrange
      mockApprove.mockReturnValue(new Promise(() => {}))
      await renderReady()
      const approve = screen.getByRole('button', { name: 'Approve' })

      // Act
      act(() => {
        approve.click()
        approve.click()
        screen.getByRole('button', { name: 'Deny' }).click()
      })

      // Assert
      expect(mockApprove).toHaveBeenCalledTimes(1)
      expect(mockDeny).not.toHaveBeenCalled()
    })

    it('disables both buttons while a decision is in flight', async () => {
      // Arrange
      mockDeny.mockReturnValue(new Promise(() => {}))
      await renderReady()

      // Act
      await click('Deny')

      // Assert
      expect(screen.getByRole('button', { name: /Approve/ })).toBeDisabled()
      expect(screen.getByRole('button', { name: /Deny/ })).toBeDisabled()
    })

    it('shows the expired state when the request was answered meanwhile', async () => {
      // Arrange
      mockApprove.mockRejectedValue(problem(404))
      await renderReady()

      // Act
      await click('Approve')

      // Assert
      expect(
        screen.getByText('This connection request is no longer valid'),
      ).toBeInTheDocument()
      expect(mockNavigate).not.toHaveBeenCalled()
    })

    it('lets the user try again after a network error', async () => {
      // Arrange
      mockApprove.mockRejectedValueOnce(networkError()).mockResolvedValueOnce({
        redirectUrl: 'https://tools.acme.example/cb?code=c1',
      })
      await renderReady()

      // Act
      await click('Approve')
      screen.getByText('Wayd could not be reached. Please try again.')
      await click('Approve')

      // Assert
      screen.getByText(/Returning you to the application/)
      expect(mockApprove).toHaveBeenCalledTimes(2)
      expect(mockNavigate).toHaveBeenCalledWith(
        'https://tools.acme.example/cb?code=c1',
      )
    })
  })

  describe('states', () => {
    it('shows the expired state for an unknown or expired request', async () => {
      // Arrange
      mockGet.mockRejectedValue(problem(404))

      // Act
      await renderPage()

      // Assert
      expect(
        screen.getByText('This connection request is no longer valid'),
      ).toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Approve' }),
      ).not.toBeInTheDocument()
    })

    it('shows the expired state without calling the API when there is no request in the URL', async () => {
      // Arrange
      withRequestParam(null)

      // Act
      await renderPage()

      // Assert
      expect(
        screen.getByText('This connection request is no longer valid'),
      ).toBeInTheDocument()
      expect(mockGet).not.toHaveBeenCalled()
    })

    it('explains that the feature is off when the flag is off', async () => {
      // Arrange
      mockFlag = { isEnabled: false, isLoading: false }
      mockGet.mockRejectedValue(problem(404))

      // Act
      await renderPage()

      // Assert
      expect(
        screen.getByText('Connecting applications is not turned on'),
      ).toBeInTheDocument()
      expect(
        screen.queryByText('This connection request is no longer valid'),
      ).not.toBeInTheDocument()
    })

    it('explains a 403', async () => {
      // Arrange
      mockGet.mockRejectedValue(problem(403))

      // Act
      await renderPage()

      // Assert
      expect(
        screen.getByText('Sign in to Wayd to answer this request'),
      ).toBeInTheDocument()
    })

    it('offers a retry after a network error, which loads the request', async () => {
      // Arrange
      mockGet
        .mockRejectedValueOnce(networkError())
        .mockResolvedValueOnce(pendingRequest())
      await renderPage()
      screen.getByText('Wayd could not be reached')

      // Act
      await click('Try again')

      // Assert
      expect(
        screen.getByRole('button', { name: 'Approve' }),
      ).toBeInTheDocument()
      expect(mockGet).toHaveBeenCalledTimes(2)
    })

    it('refuses to show the request inside a frame', async () => {
      // Arrange
      mockFramed = true
      mockGet.mockResolvedValue(pendingRequest())

      // Act
      await renderPage()

      // Assert
      expect(
        screen.getByText('This page cannot be shown inside another site'),
      ).toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Approve' }),
      ).not.toBeInTheDocument()
      expect(mockGet).not.toHaveBeenCalled()
    })
  })

  describe('approve delay', () => {
    it('keeps Approve disabled until the page has shown for a moment, but not Deny', async () => {
      // Arrange
      mockGet.mockResolvedValue(pendingRequest())
      await renderPage()

      // Act
      const before = screen.getByRole('button', { name: 'Approve' })
      const beforeDisabled = (before as HTMLButtonElement).disabled
      await advance(APPROVE_DELAY_MS)

      // Assert
      expect(beforeDisabled).toBe(true)
      expect(screen.getByRole('button', { name: 'Deny' })).toBeEnabled()
      expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled()
    })

    it('disables Approve when the window loses focus and re-enables it a moment after focus returns', async () => {
      // Arrange
      await renderReady()

      // Act
      await act(async () => {
        window.dispatchEvent(new Event('blur'))
      })
      const afterBlur = screen.getByRole('button', { name: 'Approve' })
      const disabledAfterBlur = (afterBlur as HTMLButtonElement).disabled
      await act(async () => {
        window.dispatchEvent(new Event('focus'))
      })
      const disabledRightAfterFocus = (
        screen.getByRole('button', { name: 'Approve' }) as HTMLButtonElement
      ).disabled
      await advance(APPROVE_DELAY_MS)

      // Assert
      expect(disabledAfterBlur).toBe(true)
      expect(disabledRightAfterFocus).toBe(true)
      expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled()
    })

    it('stays disabled while the page is hidden and re-arms once it is visible', async () => {
      // Arrange
      await renderReady()

      // Act
      setVisibility('hidden')
      await act(async () => {
        document.dispatchEvent(new Event('visibilitychange'))
      })
      await advance(APPROVE_DELAY_MS)
      const disabledWhileHidden = (
        screen.getByRole('button', { name: 'Approve' }) as HTMLButtonElement
      ).disabled
      setVisibility('visible')
      await act(async () => {
        document.dispatchEvent(new Event('visibilitychange'))
      })
      await advance(APPROVE_DELAY_MS)

      // Assert
      expect(disabledWhileHidden).toBe(true)
      expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled()
    })
  })

  describe('server errors', () => {
    it('distinguishes a server error from an unreachable server', async () => {
      // Arrange
      mockGet
        .mockRejectedValueOnce(problem(500))
        .mockResolvedValueOnce(pendingRequest())
      await renderPage()

      // Act
      const shown = screen.queryByText('Something went wrong') !== null
      const unreachable =
        screen.queryByText('Wayd could not be reached') !== null
      await click('Try again')

      // Assert
      expect(shown).toBe(true)
      expect(unreachable).toBe(false)
      expect(screen.getByRole('button', { name: 'Deny' })).toBeInTheDocument()
    })

    it('lets the user try again after a server error on a decision', async () => {
      // Arrange
      mockDeny.mockRejectedValueOnce(problem(500)).mockResolvedValueOnce({
        redirectUrl: 'https://tools.acme.example/cb?error=access_denied',
      })
      await renderReady()

      // Act
      await click('Deny')
      const message =
        screen.queryByText(
          'Wayd could not complete that. Please try again.',
        ) !== null
      await click('Deny')

      // Assert
      expect(message).toBe(true)
      expect(mockDeny).toHaveBeenCalledTimes(2)
      expect(mockNavigate).toHaveBeenCalledWith(
        'https://tools.acme.example/cb?error=access_denied',
      )
    })
  })

  it('offers a way back into Wayd from a request that is no longer valid', async () => {
    // Arrange
    mockGet.mockRejectedValue(problem(404))

    // Act
    await renderPage()

    // Assert
    expect(screen.getByRole('link', { name: 'Go to Wayd' })).toHaveAttribute(
      'href',
      '/',
    )
  })
})
