import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import { demandManagementApi } from './demand-management-api'

const getIntakeRequests = jest.fn()
const getIntakeRequest = jest.fn()
const update = jest.fn()
const addParticipant = jest.fn()
const reroute = jest.fn()
const create = jest.fn()
const getIntakeDesk = jest.fn()
const changeStatus = jest.fn()
const withdraw = jest.fn()
const getStatusHistory = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getIntakeRequestsClient: () => ({
    getIntakeRequests,
    getIntakeRequest,
    update,
    addParticipant,
    reroute,
    create,
    changeStatus,
    withdraw,
    getStatusHistory,
  }),
  getIntakeDesksClient: () => ({ getIntakeDesk }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const details = (id: string) => ({
  summary: { id, key: 7, title: 'An ask' },
  substance: null,
  canEdit: false,
  canRemoveParticipants: false,
  canDelete: false,
  canAddParticipants: false,
})

describe('demandManagementApi', () => {
  beforeEach(() => {
    jest.resetAllMocks()
  })

  describe('getIntakeRequests', () => {
    it('asks the server for my requests rather than filtering the summaries', async () => {
      // Arrange
      getIntakeRequests.mockResolvedValue([])
      const store = createStore()

      // Act
      await store.dispatch(
        demandManagementApi.endpoints.getIntakeRequests.initiate({
          mine: true,
        }),
      )

      // Assert
      expect(getIntakeRequests).toHaveBeenCalledWith(undefined, true)
    })
  })

  describe('getIntakeRequest', () => {
    it('reports a request the reader cannot see as a 404', async () => {
      // Arrange
      getIntakeRequest.mockRejectedValue({ status: 404, title: 'Not Found' })
      const store = createStore()

      // Act
      const result = await store.dispatch(
        demandManagementApi.endpoints.getIntakeRequest.initiate('7'),
      )

      // Assert
      expect(result.error).toMatchObject({ status: 404 })
    })

    it('refetches a request opened by key when a change is made by its id', async () => {
      // Arrange
      getIntakeRequest.mockResolvedValue(details('r-1'))
      // The generated client resolves a 204 as null.
      addParticipant.mockResolvedValue(null)
      const store = createStore()
      const subscription = store.dispatch(
        demandManagementApi.endpoints.getIntakeRequest.initiate('7'),
      )
      await subscription

      // Act
      await store.dispatch(
        demandManagementApi.endpoints.addIntakeRequestParticipant.initiate({
          requestId: 'r-1',
          employeeId: 'e-1',
        }),
      )
      await new Promise((resolve) => setTimeout(resolve, 0))

      // Assert
      expect(addParticipant).toHaveBeenCalledWith('r-1', { employeeId: 'e-1' })
      expect(getIntakeRequest).toHaveBeenCalledTimes(2)
      subscription.unsubscribe()
    })
  })

  describe('a change to where requests are', () => {
    it.each([
      [
        'moving a request',
        (store: ReturnType<typeof createStore>) =>
          store.dispatch(
            demandManagementApi.endpoints.rerouteIntakeRequest.initiate({
              requestId: 'r-1',
              deskId: 'd-2',
              confirmWiderVisibility: false,
            }),
          ),
      ],
      [
        'submitting a request',
        (store: ReturnType<typeof createStore>) =>
          store.dispatch(
            demandManagementApi.endpoints.createIntakeRequest.initiate({
              title: 'An ask',
              description: 'Something',
              deskId: 'd-1',
              channelId: 'c-1',
            }),
          ),
      ],
      [
        'changing the status of a request',
        (store: ReturnType<typeof createStore>) =>
          store.dispatch(
            demandManagementApi.endpoints.changeIntakeRequestStatus.initiate({
              requestId: 'r-1',
              statusId: 's-2',
              reason: 'Budget went elsewhere',
            }),
          ),
      ],
      [
        'withdrawing a request',
        (store: ReturnType<typeof createStore>) =>
          store.dispatch(
            demandManagementApi.endpoints.withdrawIntakeRequest.initiate({
              requestId: 'r-1',
            }),
          ),
      ],
    ])(
      'refreshes a desk page already open after %s, whose waiting count and delete flag move',
      async (_change, dispatchChange) => {
        // Arrange
        getIntakeDesk.mockResolvedValue({ id: 'd-1', key: 1 })
        reroute.mockResolvedValue(null)
        changeStatus.mockResolvedValue(null)
        withdraw.mockResolvedValue(null)
        create.mockResolvedValue({ id: 'r-2', key: 8 })
        const store = createStore()
        const subscription = store.dispatch(
          demandManagementApi.endpoints.getIntakeDesk.initiate('1'),
        )
        await subscription

        // Act
        await dispatchChange(store)
        await new Promise((resolve) => setTimeout(resolve, 0))

        // Assert
        expect(getIntakeDesk).toHaveBeenCalledTimes(2)
        subscription.unsubscribe()
      },
    )
  })

  describe('getIntakeRequestStatusHistory', () => {
    it('refetches the history once the request moves to another status', async () => {
      // Arrange
      getStatusHistory.mockResolvedValue([])
      changeStatus.mockResolvedValue(null)
      const store = createStore()
      const subscription = store.dispatch(
        demandManagementApi.endpoints.getIntakeRequestStatusHistory.initiate(
          'r-1',
        ),
      )
      await subscription

      // Act
      await store.dispatch(
        demandManagementApi.endpoints.changeIntakeRequestStatus.initiate({
          requestId: 'r-1',
          statusId: 's-2',
        }),
      )
      await new Promise((resolve) => setTimeout(resolve, 0))

      // Assert
      expect(changeStatus).toHaveBeenCalledWith('r-1', { statusId: 's-2' })
      expect(getStatusHistory).toHaveBeenCalledTimes(2)
      subscription.unsubscribe()
    })
  })

  describe('updateIntakeRequest', () => {
    it('returns a validation failure as the client threw it, where toApiError finds its errors', async () => {
      // Arrange
      const problem = { status: 422, errors: { Title: ['Title is required.'] } }
      update.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        demandManagementApi.endpoints.updateIntakeRequest.initiate({
          id: 'r-1',
          title: '',
          description: 'Something',
          channelId: 'c-1',
        }),
      )

      // Assert
      expect(result.error).toEqual(problem)
    })
  })
})
