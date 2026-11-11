// @vitest-environment jsdom
import { configureStore } from '@reduxjs/toolkit'
import { PokerSessionDetailsDto } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { pokerSessionsApi, setCachedFocusedRound } from './poker-sessions-api'

const getSession = vi.fn()
const focusRound = vi.fn()

vi.mock(import('@/src/services/clients'), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    // These endpoints call only the two methods stubbed here.
    getPokerSessionsClient: (() => ({
      getSession,
      focusRound,
    })) as unknown as typeof actual.getPokerSessionsClient,
  }
})

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

type Store = ReturnType<typeof createStore>

const session = (key: number, focusedRoundId?: string) =>
  ({ id: `session-${key}`, key, focusedRoundId }) as PokerSessionDetailsDto

const load = (store: Store, idOrKey: string) =>
  store.dispatch(pokerSessionsApi.endpoints.getPokerSession.initiate(idOrKey))

const cachedFocus = (store: Store, idOrKey: string) =>
  pokerSessionsApi.endpoints.getPokerSession.select(idOrKey)(store.getState())
    .data?.focusedRoundId

/** A promise the test settles by hand, to look at the cache mid-request. */
const deferred = () => {
  let resolve!: () => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<void>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

describe('pokerSessionsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('setCachedFocusedRound', () => {
    it('writes the round into every cached copy of the session', async () => {
      // Arrange
      getSession.mockResolvedValue(session(7))
      const store = createStore()
      await load(store, '7')
      await load(store, 'session-7')

      // Act
      setCachedFocusedRound(store.dispatch, store.getState(), 7, 'round-2')

      // Assert
      expect(cachedFocus(store, '7')).toBe('round-2')
      expect(cachedFocus(store, 'session-7')).toBe('round-2')
    })

    it('leaves other sessions alone', async () => {
      // Arrange
      getSession.mockImplementation(async (idOrKey: string) =>
        session(Number(idOrKey), 'round-1'),
      )
      const store = createStore()
      await load(store, '7')
      await load(store, '8')

      // Act
      setCachedFocusedRound(store.dispatch, store.getState(), 7, 'round-2')

      // Assert
      expect(cachedFocus(store, '8')).toBe('round-1')
    })

    it('clears the focus when no round is given', async () => {
      // Arrange
      getSession.mockResolvedValue(session(7, 'round-1'))
      const store = createStore()
      await load(store, '7')

      // Act
      setCachedFocusedRound(store.dispatch, store.getState(), 7, undefined)

      // Assert
      expect(cachedFocus(store, '7')).toBeUndefined()
    })
  })

  describe('focusPokerRound', () => {
    const focus = (store: Store, roundId: string | undefined) =>
      store.dispatch(
        pokerSessionsApi.endpoints.focusPokerRound.initiate({
          sessionId: 'session-7',
          sessionKey: 7,
          roundId,
        }),
      )

    it('shows the round while the request is still in flight', async () => {
      // Arrange
      getSession.mockResolvedValue(session(7, 'round-1'))
      const store = createStore()
      await load(store, '7')
      const request = deferred()
      focusRound.mockReturnValue(request.promise)
      getSession.mockReturnValue(new Promise(() => {}))

      // Act
      const pending = focus(store, 'round-2')

      // Assert
      expect(cachedFocus(store, '7')).toBe('round-2')
      request.resolve()
      await pending
      expect(focusRound).toHaveBeenCalledWith('session-7', {
        roundId: 'round-2',
      })
    })

    it('puts the previous round back when the request fails', async () => {
      // Arrange
      getSession.mockResolvedValue(session(7, 'round-1'))
      const store = createStore()
      await load(store, '7')
      focusRound.mockRejectedValue({ status: 400, detail: 'Round not found.' })
      getSession.mockReturnValue(new Promise(() => {}))

      // Act
      await focus(store, 'round-2')

      // Assert
      expect(cachedFocus(store, '7')).toBe('round-1')
    })

    it('reads the session again, so an older read cannot leave the previous round showing', async () => {
      // Arrange
      getSession.mockResolvedValue(session(7, 'round-1'))
      const store = createStore()
      await load(store, '7')
      focusRound.mockResolvedValue(undefined)
      getSession.mockResolvedValue(session(7, 'round-2'))

      // Act
      await focus(store, 'round-2')

      // Assert
      await vi.waitFor(() => expect(getSession).toHaveBeenCalledTimes(2))
      expect(cachedFocus(store, '7')).toBe('round-2')
    })
  })
})
