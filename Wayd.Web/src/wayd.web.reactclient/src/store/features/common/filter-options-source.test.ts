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
      if (search === 'slow') {
        await new Promise((_, reject) =>
          signal.addEventListener('abort', () => reject(new Error('aborted'))),
        )
      }
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

  it('aborts the request when the panel’s signal aborts', async () => {
    // Arrange
    const { load, queryFn } = setup()
    const controller = new AbortController()
    const pending = load({ search: 'slow', signal: controller.signal })
    await vi.waitFor(() => expect(queryFn).toHaveBeenCalled())

    // Act
    controller.abort()

    // Assert
    await expect(pending).rejects.toBeDefined()
    expect(queryFn.mock.calls[0][1].signal.aborted).toBe(true)
  })
})
