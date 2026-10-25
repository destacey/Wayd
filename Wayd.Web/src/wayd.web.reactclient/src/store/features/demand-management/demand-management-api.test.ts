import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import { demandManagementApi } from './demand-management-api'

const getIntakeRequests = jest.fn()
const getIntakeRequest = jest.fn()
const update = jest.fn()
const addParticipant = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getIntakeRequestsClient: () => ({
    getIntakeRequests,
    getIntakeRequest,
    update,
    addParticipant,
  }),
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
  canReview: false,
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

  describe('updateIntakeRequest', () => {
    it('returns a validation failure as status and data, where the form reads its errors', async () => {
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
      expect(result.error).toEqual({ status: 422, data: problem })
    })
  })
})
