'use client'

import { useState } from 'react'
import type { WorkItemDependencyFilter } from './work-item-dependency-neighbourhood'

export const WORK_ITEM_DEPENDENCY_FILTER_KEY =
  'wayd.workItemDependencyMap.filter'

const read = (): WorkItemDependencyFilter => {
  if (typeof window === 'undefined') return 'open'

  try {
    return window.sessionStorage.getItem(WORK_ITEM_DEPENDENCY_FILTER_KEY) ===
      'all'
      ? 'all'
      : 'open'
  } catch {
    return 'open'
  }
}

/**
 * The map's filter, kept for the browser session. Open only by default: a done link no longer holds
 * anything up, and a long history of them buries the few that still do.
 *
 * Session rather than local storage: someone chasing what blocks a release moves from item to item and
 * should not have to reapply it on each, but a choice that outlives the session is one a reader forgets
 * they made. Storage can throw or be empty (private windows, blocked site data), in which case the map
 * falls back to the default.
 */
export const useWorkItemDependencyFilter = (): [
  WorkItemDependencyFilter,
  (filter: WorkItemDependencyFilter) => void,
] => {
  const [filter, setFilter] = useState<WorkItemDependencyFilter>(read)

  const update = (next: WorkItemDependencyFilter) => {
    setFilter(next)
    try {
      window.sessionStorage.setItem(WORK_ITEM_DEPENDENCY_FILTER_KEY, next)
    } catch {
      // Remembering the choice is a convenience; the map is already showing it.
    }
  }

  return [filter, update]
}
