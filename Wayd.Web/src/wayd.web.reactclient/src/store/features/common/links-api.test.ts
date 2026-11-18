// @vitest-environment jsdom
import { configureStore } from '@reduxjs/toolkit'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { linksApi, LinkOwner, LinkOwnerTypes } from './links-api'

const getLinks = vi.fn()
const create = vi.fn()
const update = vi.fn()
const remove = vi.fn()

vi.mock(import('@/src/services/api/links'), async (importOriginal) => ({
  ...(await importOriginal()),
  linksGetLinks: (...args: unknown[]) => getLinks(...args),
  linksCreate: (...args: unknown[]) => create(...args),
  linksUpdate: (...args: unknown[]) => update(...args),
  linksDelete: (...args: unknown[]) => remove(...args),
}))

const probe = vi.fn()

/** Stand-ins for the queries that show a record's Activity and an incident's timeline. */
const probesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    linkTestActivity: builder.query<null, string>({
      queryFn: (id) => {
        probe('activity', id)
        return { data: null }
      },
      providesTags: (result, error, id) => [
        { type: QueryTags.ActivityLog, id },
      ],
    }),
    linkTestTimeline: builder.query<null, string>({
      queryFn: (id) => {
        probe('timeline', id)
        return { data: null }
      },
      providesTags: (result, error, id) => [
        { type: QueryTags.IncidentTimelineLinkEntries, id },
      ],
    }),
  }),
})

const createStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefault) => getDefault().concat(apiSlice.middleware),
  })

const project: LinkOwner = {
  ownerType: LinkOwnerTypes.Project,
  ownerId: 'project-1',
}
const incident: LinkOwner = {
  ownerType: LinkOwnerTypes.Incident,
  ownerId: 'incident-1',
}
const request = { name: 'Wiki', url: 'https://example.com/wiki' }

/** Loads an owner's links, so the test can count how often a change refetches them. */
const load = async (store: ReturnType<typeof createStore>, owner: LinkOwner) =>
  store.dispatch(linksApi.endpoints.getLinks.initiate(owner))

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

describe('linksApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getLinks.mockResolvedValue({ canManage: true, links: [] })
    create.mockResolvedValue('link-2')
    update.mockResolvedValue(undefined)
    remove.mockResolvedValue(undefined)
  })

  it('reads and changes links by owner type, owner id and link id', async () => {
    // Arrange
    const store = createStore()

    // Act
    await load(store, project)
    await store.dispatch(
      linksApi.endpoints.createLink.initiate({ ...project, request }),
    )
    await store.dispatch(
      linksApi.endpoints.updateLink.initiate({
        ...project,
        linkId: 'link-1',
        request,
      }),
    )
    await store.dispatch(
      linksApi.endpoints.deleteLink.initiate({ ...project, linkId: 'link-1' }),
    )

    // Assert
    expect(getLinks).toHaveBeenCalledWith('ppm.project', 'project-1')
    expect(create).toHaveBeenCalledWith('ppm.project', 'project-1', request)
    expect(update).toHaveBeenCalledWith(
      'ppm.project',
      'project-1',
      'link-1',
      request,
    )
    expect(remove).toHaveBeenCalledWith('ppm.project', 'project-1', 'link-1')
  })

  type Store = ReturnType<typeof createStore>

  it.each<[string, (store: Store) => PromiseLike<unknown>]>([
    [
      'adding',
      (store) =>
        store.dispatch(
          linksApi.endpoints.createLink.initiate({ ...project, request }),
        ),
    ],
    [
      'changing',
      (store) =>
        store.dispatch(
          linksApi.endpoints.updateLink.initiate({
            ...project,
            linkId: 'link-1',
            request,
          }),
        ),
    ],
    [
      'removing',
      (store) =>
        store.dispatch(
          linksApi.endpoints.deleteLink.initiate({
            ...project,
            linkId: 'link-1',
          }),
        ),
    ],
  ])(
    "refetches that record's links, and no other record's, after %s one",
    async (_, change) => {
      // Arrange
      const store = createStore()
      await load(store, project)
      await load(store, incident)
      getLinks.mockClear()

      // Act
      await change(store)
      await flush()

      // Assert
      expect(getLinks).toHaveBeenCalledTimes(1)
      expect(getLinks).toHaveBeenCalledWith('ppm.project', 'project-1')
    },
  )

  it('refetches nothing when the change is refused', async () => {
    // Arrange
    const store = createStore()
    await load(store, project)
    getLinks.mockClear()
    create.mockRejectedValue({ status: 403 })

    // Act
    const result = await store.dispatch(
      linksApi.endpoints.createLink.initiate({ ...project, request }),
    )
    await flush()

    // Assert
    expect('error' in result).toBe(true)
    expect(getLinks).not.toHaveBeenCalled()
  })

  it("refreshes the record's activity after a change, and an incident's timeline too", async () => {
    // Arrange
    const store = createStore()
    for (const owner of [project, incident]) {
      await store.dispatch(
        probesApi.endpoints.linkTestActivity.initiate(owner.ownerId),
      )
      await store.dispatch(
        probesApi.endpoints.linkTestTimeline.initiate(owner.ownerId),
      )
    }
    probe.mockClear()

    // Act
    await store.dispatch(
      linksApi.endpoints.createLink.initiate({ ...project, request }),
    )
    await flush()
    const afterProject = probe.mock.calls.map((c) => c.join(':'))
    probe.mockClear()
    await store.dispatch(
      linksApi.endpoints.createLink.initiate({ ...incident, request }),
    )
    await flush()
    const afterIncident = probe.mock.calls.map((c) => c.join(':')).sort()

    // Assert
    expect(afterProject).toEqual(['activity:project-1'])
    expect(afterIncident).toEqual([
      'activity:incident-1',
      'timeline:incident-1',
    ])
  })
})
