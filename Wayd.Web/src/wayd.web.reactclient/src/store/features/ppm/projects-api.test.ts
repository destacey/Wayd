import { configureStore } from '@reduxjs/toolkit'
import { JsonPatchOperationType } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { projectsApi } from './projects-api'

const patchProjectStage = jest.fn()

jest.mock('@/src/services/clients', () => ({
  getProjectsClient: () => ({ patchProjectStage }),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const operations = [
  { op: JsonPatchOperationType.Replace, path: '/Progress', value: 40 },
]

describe('projectsApi', () => {
  beforeEach(() => {
    patchProjectStage.mockReset()
  })

  describe('patchProjectStage', () => {
    it('sends the operations through the generated client', async () => {
      // Arrange
      patchProjectStage.mockResolvedValue(null)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        projectsApi.endpoints.patchProjectStage.initiate({
          projectId: 'p-1',
          projectKey: 'APOLLO',
          stageId: 's-1',
          patchOperations: operations,
        }),
      )

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(patchProjectStage).toHaveBeenCalledWith('p-1', 's-1', operations)
    })

    it('returns a validation failure as status and data, where the form reads its errors', async () => {
      // Arrange
      const problem = {
        status: 422,
        errors: { Progress: ['Progress must be between 0 and 100.'] },
      }
      patchProjectStage.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(
        projectsApi.endpoints.patchProjectStage.initiate({
          projectId: 'p-1',
          projectKey: 'APOLLO',
          stageId: 's-1',
          patchOperations: operations,
        }),
      )

      // Assert
      expect(result.error).toEqual({ status: 422, data: problem })
    })
  })
})
