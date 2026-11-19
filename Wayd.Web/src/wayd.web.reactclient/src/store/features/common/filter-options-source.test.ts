import { configureStore } from '@reduxjs/toolkit'
import { createApi, fakeBaseQuery } from '@reduxjs/toolkit/query'
import type { ListFilterOptionsDto } from '@/src/services/api/wayd.schemas'
import type { AppDispatch } from '../../store'
import { filterOptionsLoader } from './filter-options-source'

const setup = () => {
  const queryFn = vi.fn(
    async (
      { search }: { search: string },
      { signal }: { signal: AbortSignal },
    ) => {
      const data: ListFilterOptionsDto = {
        options: [{ value: `id-${search}`, label: `Label ${search}` }],
        hasMore: search === 'many',
      }
      return { data }
    },
  )
  const api = createApi({
    reducerPath: 'testApi',
    baseQuery: fakeBaseQuery(),
    endpoints: (builder) => ({
      options: builder.query<ListFilterOptionsDto, { search: string }>({
        queryFn,
      }),
    }),
  })
  const store = configureStore({
    reducer: { [api.reducerPath]: api.reducer },
    middleware: (getDefault) => getDefault().concat(api.middleware),
  })
  const load = filterOptionsLoader(
    store.dispatch as AppDispatch,
    api.endpoints.options,
    (search) => ({ search }),
  )
  return { queryFn, load }
}

describe('filterOptionsLoader', () => {
  it('answers with the endpoint’s options and whether there are more', async () => {
    // Arrange
    const { load } = setup()

    // Act
    const page = await load({
      search: 'many',
      signal: new AbortController().signal,
    })

    // Assert
    expect(page).toEqual({
      options: [{ value: 'id-many', label: 'Label many' }],
      hasMore: true,
    })
  })

  it('answers a search it has answered from the cache', async () => {
    // Arrange
    const { load, queryFn } = setup()
    await load({ search: 'ada', signal: new AbortController().signal })

    // Act
    const again = await load({
      search: 'ada',
      signal: new AbortController().signal,
    })

    // Assert
    expect(again.options[0].label).toBe('Label ada')
    expect(queryFn).toHaveBeenCalledTimes(1)
  })

  it('asks again for a different search', async () => {
    // Arrange
    const { load, queryFn } = setup()
    await load({ search: 'ada', signal: new AbortController().signal })

    // Act
    await load({ search: 'grace', signal: new AbortController().signal })

    // Assert
    expect(queryFn).toHaveBeenCalledTimes(2)
  })

  it('answers a search the panel cancels and asks for again at once', async () => {
    // Arrange — React's StrictMode mounts the panel, cleans it up and mounts it again
    const { load, queryFn } = setup()
    const first = new AbortController()
    const cancelled = load({ search: 'slow-free', signal: first.signal })
    first.abort()

    // Act
    const page = await load({
      search: 'slow-free',
      signal: new AbortController().signal,
    })

    // Assert
    expect(page.options[0].label).toBe('Label slow-free')
    await expect(cancelled).resolves.toBeDefined()
    expect(queryFn).toHaveBeenCalledTimes(1)
    expect(queryFn.mock.calls[0][1].signal.aborted).toBe(false)
  })
})
