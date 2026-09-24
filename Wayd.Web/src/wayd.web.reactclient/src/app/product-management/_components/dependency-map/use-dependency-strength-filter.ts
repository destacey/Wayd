'use client'

import { useState } from 'react'
import type { DependencyStrengthFilter } from './product-dependency-neighbourhood'

export const DEPENDENCY_STRENGTH_FILTER_KEY =
  'wayd.dependencyMap.strengthFilter'

const read = (): DependencyStrengthFilter => {
  if (typeof window === 'undefined') return 'all'

  try {
    return window.sessionStorage.getItem(DEPENDENCY_STRENGTH_FILTER_KEY) ===
      'hard'
      ? 'hard'
      : 'all'
  } catch {
    return 'all'
  }
}

/**
 * The map's strength filter, kept for the browser session.
 *
 * Session rather than local storage: someone tracing an outage moves from product to product and should
 * not have to reapply it on each, but a filter that outlives the session is one a reader forgets is on and
 * reads a partial map as the whole one. Storage can throw or be empty (private windows, blocked site
 * data), in which case the map shows everything.
 */
export const useDependencyStrengthFilter = (): [
  DependencyStrengthFilter,
  (filter: DependencyStrengthFilter) => void,
] => {
  const [filter, setFilter] = useState<DependencyStrengthFilter>(read)

  const update = (next: DependencyStrengthFilter) => {
    setFilter(next)
    try {
      window.sessionStorage.setItem(DEPENDENCY_STRENGTH_FILTER_KEY, next)
    } catch {
      // Remembering the choice is a convenience; the map is already showing it.
    }
  }

  return [filter, update]
}
