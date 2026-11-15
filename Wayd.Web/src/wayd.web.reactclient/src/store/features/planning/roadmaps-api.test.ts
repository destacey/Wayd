import { configureStore } from '@reduxjs/toolkit'
import { JsonPatchOperationType } from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'
import { roadmapApi } from './roadmaps-api'

const patchRoadmapItem = vi.fn()

vi.mock(import('@/src/services/api/roadmaps'), async (importOriginal) => ({
  ...(await importOriginal()),
  roadmapsPatchRoadmapItem: (...args: unknown[]) => patchRoadmapItem(...args),
}))

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const operations = [
  { op: JsonPatchOperationType.replace, path: '/end', value: '2026-06-30' },
]

const patch = () =>
  roadmapApi.endpoints.patchRoadmapItem.initiate({
    roadmapId: 'r-1',
    itemId: 'i-1',
    patchOperations: operations,
  })

describe('roadmapApi', () => {
  beforeEach(() => {
    patchRoadmapItem.mockReset()
  })

  describe('patchRoadmapItem', () => {
    it('sends the operations through the generated client', async () => {
      // Arrange
      patchRoadmapItem.mockResolvedValue(null)
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toBeUndefined()
      expect(result.data).toBeNull()
      expect(patchRoadmapItem).toHaveBeenCalledWith('r-1', 'i-1', operations)
    })

    it('returns a validation failure as status and data, where the grid reads its errors', async () => {
      // Arrange
      const problem = {
        status: 422,
        errors: { End: ['End must be after Start.'] },
      }
      patchRoadmapItem.mockRejectedValue(problem)
      const store = createStore()

      // Act
      const result = await store.dispatch(patch())

      // Assert
      expect(result.error).toEqual({ status: 422, data: problem })
    })
  })
})
