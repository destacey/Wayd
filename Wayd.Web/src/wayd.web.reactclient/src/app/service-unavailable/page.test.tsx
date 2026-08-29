import { render, screen, fireEvent, act } from '@testing-library/react'
import ServiceUnavailablePage from './page'

// Suppress Ant Design act() warnings from async state updates
const originalError = console.error
beforeAll(() => {
  console.error = (...args: any[]) => {
    if (typeof args[0] === 'string' && args[0].includes('act(')) return
    originalError.call(console, ...args)
  }
})
afterAll(() => {
  console.error = originalError
})

// Mock fetch response (Response class is not available in jsdom)
const mockOkResponse = { ok: true, status: 200 }
const mock404Response = { ok: false, status: 404 }

describe('ServiceUnavailablePage', () => {
  const mockFetch = jest.fn()
  const originalFetch = global.fetch

  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_BASE_URL = 'https://localhost:5001'
    global.fetch = mockFetch
  })

  afterEach(() => {
    mockFetch.mockReset()
    global.fetch = originalFetch
    delete process.env.NEXT_PUBLIC_API_BASE_URL
  })

  describe('when API is unreachable', () => {
    beforeEach(() => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))
    })

    it('shows "Service Unavailable" title', async () => {
      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(screen.getByText('Wayd API Unavailable')).toBeInTheDocument()
    })

    it('shows server connectivity messaging', async () => {
      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(
        screen.getByText(
          'Wayd is unable to connect to the server. Please try again later.',
        ),
      ).toBeInTheDocument()
      expect(
        screen.getByText('The server is temporarily unavailable'),
      ).toBeInTheDocument()
    })
  })

  describe('when API is reachable', () => {
    beforeEach(() => {
      mockFetch.mockResolvedValue(mockOkResponse)
    })

    it('shows "Session Error" title', async () => {
      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(screen.getByText('Session Error')).toBeInTheDocument()
    })

    it('shows auth-related messaging', async () => {
      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(
        screen.getByText(/your session could not be verified/i),
      ).toBeInTheDocument()
      expect(
        screen.getByText(
          /your session expired while the app was open in multiple tabs/i,
        ),
      ).toBeInTheDocument()
    })

    it('treats non-200 responses as reachable (auth issue)', async () => {
      // A 404 still means the server is reachable
      mockFetch.mockResolvedValue(mock404Response)

      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(screen.getByText('Session Error')).toBeInTheDocument()
    })
  })

  describe('Retry button', () => {
    beforeEach(() => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))
    })

    it('calls onRetry when provided', async () => {
      const onRetry = jest.fn()
      await act(async () => {
        render(<ServiceUnavailablePage onRetry={onRetry} />)
      })

      fireEvent.click(screen.getByText('Retry'))
      expect(onRetry).toHaveBeenCalledTimes(1)
    })
  })

  describe('Sign Out button', () => {
    beforeEach(() => {
      mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))
    })

    it('calls onLogout when provided', async () => {
      const onLogout = jest.fn()
      await act(async () => {
        render(<ServiceUnavailablePage onLogout={onLogout} />)
      })

      fireEvent.click(screen.getByText('Sign Out'))
      expect(onLogout).toHaveBeenCalledTimes(1)
    })

    it('is always visible regardless of API status', async () => {
      mockFetch.mockResolvedValue(mockOkResponse)

      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(
        screen.getByRole('button', { name: /sign out/i }),
      ).toBeInTheDocument()
    })
  })

  describe('health check request', () => {
    it('calls the /alive endpoint on the API base URL', async () => {
      mockFetch.mockResolvedValue(mockOkResponse)

      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(mockFetch).toHaveBeenCalledWith(
        'https://localhost:5001/alive',
        expect.objectContaining({ method: 'GET' }),
      )
    })

    it('passes an AbortSignal to the fetch call', async () => {
      mockFetch.mockResolvedValue(mockOkResponse)

      await act(async () => {
        render(<ServiceUnavailablePage />)
      })

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          signal: expect.any(AbortSignal),
        }),
      )
    })
  })
})
