import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import { teamApi } from './team-api'

const getActiveSprint = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getTeamsClient: () => ({ getActiveSprint }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

describe('teamApi', () => {
  beforeEach(() => {
    getActiveSprint.mockReset()
    jest.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('getActiveSprint', () => {
    it('returns the sprint the API answers with', async () => {
      // Arrange
      const sprint = { id: 'sprint-1', key: 12, name: 'Sprint 12' }
      getActiveSprint.mockResolvedValue(sprint)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toEqual(sprint)
      expect(getActiveSprint).toHaveBeenCalledWith('team-1')
    })

    it('reads a 404 as the team having no active sprint', async () => {
      // Arrange
      getActiveSprint.mockRejectedValue({ status: 404, title: 'Not Found' })
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
    })

    it('keeps any other failure as an error', async () => {
      // Arrange
      const problem = { status: 500, title: 'Internal Server Error' }
      getActiveSprint.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        teamApi.endpoints.getActiveSprint.initiate('team-1'),
      )

      // Assert
      expect(result.data).toBeUndefined()
      expect(result.error).toEqual(problem)
    })
  })
})
