'use client'

import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useCallback } from 'react'

/** The query param carrying the open record. */
export const SELECTED_PARAM = 'selected'

export interface SelectedRecordState {
  /** The selected record's id, or null when nothing is open. */
  selectedId: string | null
  /** Opens a record. */
  select: (id: string) => void
  /** Closes the panel. */
  clear: () => void
}

/**
 * Reads and writes the config list's open record as a URL query param.
 *
 * Deliberately in the URL, where a record section's filter is not: which
 * record you are looking at is a *place*, so it should survive a refresh, be
 * reachable by Back, and paste into a ticket. A filter is a working preference
 * and stays in local storage — see `useStatusFilter` for that side of the rule.
 *
 * `replace`, not `push`: stepping through six config rows should leave Back
 * pointing at wherever the user came from, not at the fifth row. Closing the
 * panel is the one exception — see `clear`.
 */
export const useSelectedRecord = (): SelectedRecordState => {
  const params = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()

  // useSearchParams returns a fresh object every render, so depending on it
  // directly would rebuild the callbacks each time. The string is stable.
  const query = params.toString()

  const write = useCallback(
    (id: string | null) => {
      // Carry the rest of the query across — the selection is one axis of page
      // state among several, and rebuilding from it alone would silently drop
      // the others.
      const next = new URLSearchParams(query)
      if (id === null) {
        next.delete(SELECTED_PARAM)
      } else {
        next.set(SELECTED_PARAM, id)
      }
      const nextQuery = next.toString()
      router.replace(nextQuery ? `${pathname}?${nextQuery}` : pathname, {
        scroll: false,
      })
    },
    [query, pathname, router],
  )

  const select = useCallback((id: string) => write(id), [write])
  const clear = useCallback(() => write(null), [write])

  return {
    selectedId: params.get(SELECTED_PARAM),
    select,
    clear,
  }
}

export default useSelectedRecord
