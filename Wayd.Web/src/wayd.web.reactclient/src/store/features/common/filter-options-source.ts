import type { FilterOptionsSource } from '@/src/components/common/wayd-grid'
import type { ListFilterOptionsDto } from '@/src/services/api/wayd.schemas'
import type { AppDispatch } from '../../store'

/**
 * An RTK Query endpoint answering a list's `filter-options` request: what
 * {@link filterOptionsLoader} needs of `api.endpoints.x`.
 */
export interface FilterOptionsEndpoint<TArgs> {
  initiate: (
    args: TArgs,
    options: { subscribe: false },
  ) => (
    dispatch: AppDispatch,
    // RTK's thunk also takes getState and an extra argument, which the loader never passes.
    ...rest: any[]
  ) => { unwrap: () => Promise<ListFilterOptionsDto>; abort: () => void }
}

/**
 * A set filter's `load` over an RTK Query endpoint, so each search is cached
 * like any other query: reopening a filter, or the same filter on another grid,
 * within the endpoint's `keepUnusedDataFor` reads the cache instead of the API.
 * The request is aborted when the panel's signal is, which a search the user
 * has typed past does.
 *
 * @param dispatch The store's dispatch (`useAppDispatch()`).
 * @param endpoint The endpoint, `incidentsApi.endpoints.getIncidentFilterOptions`.
 * @param argsFor The endpoint's arguments for a search.
 */
export const filterOptionsLoader =
  <TArgs>(
    dispatch: AppDispatch,
    endpoint: FilterOptionsEndpoint<TArgs>,
    argsFor: (search: string) => TArgs,
  ): FilterOptionsSource['load'] =>
  async ({ search, signal }) => {
    const request = dispatch(
      endpoint.initiate(argsFor(search), { subscribe: false }),
    )
    signal.addEventListener('abort', () => request.abort(), { once: true })
    const { options, hasMore } = await request.unwrap()
    return { options: [...options], hasMore }
  }
