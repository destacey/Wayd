import { configureStore } from '@reduxjs/toolkit'
import { JsonPatchOperationType, ProjectStatus } from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { projectDeliverablesApi } from './project-deliverables-api'
import { projectsApi } from './projects-api'

const patchProjectStage = vi.fn()
const getMyProjectTasks = vi.fn()
const getMyProjectDeliverables = vi.fn()
const lifecycle = {
  approve: vi.fn(),
  activate: vi.fn(),
  complete: vi.fn(),
  cancel: vi.fn(),
  revertStatus: vi.fn(),
  changeKey: vi.fn(),
  delete: vi.fn(),
}

vi.mock('@/src/services/clients', () => ({
  getProjectsClient: () => ({
    patchProjectStage,
    getMyProjectTasks,
    getMyProjectDeliverables,
    ...lifecycle,
  }),
  getProjectDeliverablesClient: () => ({}),
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
    vi.clearAllMocks()
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

  describe('project status, key and delete mutations', () => {
    type Store = ReturnType<typeof createStore>
    const id = 'p-1'
    const mutations: [string, (store: Store) => Promise<unknown>][] = [
      [
        'approve',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.approveProject.initiate({
              id,
              cacheKey: 'APOLLO',
            }),
          ),
      ],
      [
        'activate',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.activateProject.initiate({
              id,
              cacheKey: 'APOLLO',
            }),
          ),
      ],
      [
        'complete',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.completeProject.initiate({
              id,
              cacheKey: 'APOLLO',
            }),
          ),
      ],
      [
        'cancel',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.cancelProject.initiate({
              id,
              cacheKey: 'APOLLO',
            }),
          ),
      ],
      [
        'revert',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.revertProjectStatus.initiate({
              id,
              cacheKey: 'APOLLO',
              toStatus: ProjectStatus.Active,
              reason: 'Reopened',
            }),
          ),
      ],
      [
        'change key',
        (st) =>
          st.dispatch(
            projectsApi.endpoints.changeProjectKey.initiate({
              id,
              request: { key: 'ARTEMIS' },
            }),
          ),
      ],
      [
        'delete',
        (st) => st.dispatch(projectsApi.endpoints.deleteProject.initiate(id)),
      ],
    ]

    it.each(mutations)(
      'refetches the caller’s open tasks and owned deliverables after %s',
      async (_, mutate) => {
        // Arrange
        getMyProjectTasks.mockResolvedValue([])
        getMyProjectDeliverables.mockResolvedValue([])
        Object.values(lifecycle).forEach((fn) => fn.mockResolvedValue(null))
        const store = createStore()
        const tasks = store.dispatch(
          projectsApi.endpoints.getMyProjectTasks.initiate(undefined),
        )
        const deliverables = store.dispatch(
          projectDeliverablesApi.endpoints.getMyProjectDeliverables.initiate(),
        )
        await Promise.all([tasks, deliverables])

        // Act
        await mutate(store)

        // Assert
        await vi.waitFor(() => {
          expect(getMyProjectTasks).toHaveBeenCalledTimes(2)
          expect(getMyProjectDeliverables).toHaveBeenCalledTimes(2)
        })
        tasks.unsubscribe()
        deliverables.unsubscribe()
      },
    )
  })
})
