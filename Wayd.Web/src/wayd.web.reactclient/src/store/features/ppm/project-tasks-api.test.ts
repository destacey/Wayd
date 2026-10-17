import { configureStore } from '@reduxjs/toolkit'
import { JsonPatchOperationType } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { projectTasksApi } from './project-tasks-api'

const patchProjectTask = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getProjectTasksClient: () => ({ patchProjectTask }),
  getProjectsClient: () => ({}),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const operations = [
  { op: JsonPatchOperationType.Replace, path: '/Name', value: 'Design' },
]

const patch = () =>
  projectTasksApi.endpoints.patchProjectTask.initiate({
    projectIdOrKey: 'APOLLO',
    taskId: 't-1',
    patchOperations: operations,
    cacheKey: 't-1',
  })

describe('projectTasksApi', () => {
  beforeEach(() => {
    patchProjectTask.mockReset()
  })

  afterEach(() => {
    jest.restoreAllMocks()
  })

  describe('patchProjectTask', () => {
    it('sends the operations through the generated client', async () => {
      // Arrange
      patchProjectTask.mockResolvedValue(null)
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(patchProjectTask).toHaveBeenCalledWith('APOLLO', 't-1', operations)
    })

    it('returns a validation failure as status and data, where the grid reads its errors', async () => {
      // Arrange
      const problem = { status: 422, errors: { Name: ['Name is required.'] } }
      patchProjectTask.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toEqual({ status: 422, data: problem })
    })

    it('reports a network failure with a detail the grid can show', async () => {
      // Arrange
      patchProjectTask.mockRejectedValue({
        isAxiosError: true,
        message: 'Network Error',
      })
      jest.spyOn(console, 'error').mockImplementation(() => {})
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toEqual({
        status: 'FETCH_ERROR',
        data: { detail: 'Network Error' },
      })
    })

    it('reports an error the client raised before sending with its message', async () => {
      // Arrange
      patchProjectTask.mockRejectedValue(
        new Error("The parameter 'id' must be defined."),
      )
      jest.spyOn(console, 'error').mockImplementation(() => {})
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toEqual({
        status: 'FETCH_ERROR',
        data: { detail: "The parameter 'id' must be defined." },
      })
    })
  })
})
