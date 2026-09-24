'use client'

import { useState } from 'react'
import type { WorkItemDependencyFilter } from './work-item-dependency-neighbourhood'

export const WORK_ITEM_DEPENDENCY_FILTER_KEY =
  'wayd.workItemDependencyMap.filter'

const read = (): WorkItemDependencyFilter => {
  if (typeof window === 'undefined') return 'all'

  try {
    return window.sessionStorage.getItem(WORK_ITEM_DEPENDENCY_FILTER_KEY) ===
      'open'
      ? 'open'
      : 'all'
  } catch {
    return 'all'
  }
}

/**
 * The map's filter, kept for the browser session.
 *
 * Session rather than local storage: someone chasing what blocks a release moves from item to item and
 * should not have to reapply it on each, but a filter that outlives the session is one a reader forgets is
 * on and reads a partial map as the whole one. Storage can throw or be empty (private windows, blocked
 * site data), in which case the map shows everything.
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
