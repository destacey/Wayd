'use client'

import { useLocalStorageState } from '@/src/hooks'

/**
 * Reads and writes a status filter, remembered per record.
 *
 * The filter is shared by a record's overview and its list sections, so a count
 * on a tile and the list it links to are always the same set — both call this
 * hook with the same key and get the same state.
 *
 * Deliberately *not* in the URL. Sections are addressable and filters are not:
 * a filter is a working preference rather than a place, and holding it in the
 * query meant every navigation had to carry it, an empty selection needed a
 * spelling of its own, and `?programStatus=` could end up on a section that had
 * nothing to do with programs. Remembering it per record gives the useful half
 * — it survives a refresh and coming back — without any of that.
 *
 * An empty selection is a real state, distinct from the defaults: it means
 * every status, which is what the filter bar renders with every button lit.
 */
export const useStatusFilter = (
  /** Unique per record and collection, e.g. `portfolio:12:programStatus`. */
  storageKey: string,
  defaults: number[],
) => {
  const [selected, setSelected] = useLocalStorageState<number[]>(
    `wayd-ppm-filter:${storageKey}`,
    defaults,
    { version: 1 },
  )

  return { selected, setSelected }
}

export default useStatusFilter
