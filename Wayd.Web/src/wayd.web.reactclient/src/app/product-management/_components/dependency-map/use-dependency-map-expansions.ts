'use client'

import { useState } from 'react'
import type { DependencyFarSide } from './dependency-neighbourhood'

/** What the reader opened on one product's map: which products are expanded, and which counts revealed. */
export interface DependencyMapExpansionState {
  usedBy: Record<string, { showAll?: boolean }>
  dependsOn: Record<string, { showAll?: boolean }>
  /** Sides where the subject's own overflow has been revealed. */
  subjectShowAll: DependencyFarSide[]
}

export const emptyExpansionState = (): DependencyMapExpansionState => ({
  usedBy: {},
  dependsOn: {},
  subjectShowAll: [],
})

export const expansionStorageKey = (subjectId: string) =>
  `wayd.dependencyMap.expansions:${subjectId}`

const read = (subjectId: string): DependencyMapExpansionState => {
  if (typeof window === 'undefined') return emptyExpansionState()

  try {
    const stored = window.sessionStorage.getItem(expansionStorageKey(subjectId))
    if (!stored) return emptyExpansionState()

    const parsed = JSON.parse(stored) as Partial<DependencyMapExpansionState>

    return {
      usedBy: parsed.usedBy ?? {},
      dependsOn: parsed.dependsOn ?? {},
      subjectShowAll: parsed.subjectShowAll ?? [],
    }
  } catch {
    return emptyExpansionState()
  }
}

/**
 * The expansions open on a product's map, kept for the browser tab and per product.
 *
 * Per product, because an expansion is a path out from one subject and means nothing centred on
 * another: moving to a different product starts that map clean, and coming back finds this one as it
 * was left. Storage can throw or be empty (private windows, blocked site data), in which case the map
 * opens with nothing expanded.
 */
export const useDependencyMapExpansions = (
  subjectId: string,
): [
  DependencyMapExpansionState,
  (next: DependencyMapExpansionState) => void,
] => {
  const [entry, setEntry] = useState(() => ({
    subjectId,
    value: read(subjectId),
  }))

  // Re-read in render rather than in an effect when the page moves to another product, so the new map
  // never draws a frame with the old product's expansions.
  let current = entry
  if (entry.subjectId !== subjectId) {
    current = { subjectId, value: read(subjectId) }
    setEntry(current)
  }

  const update = (next: DependencyMapExpansionState) => {
    setEntry({ subjectId, value: next })
    try {
      window.sessionStorage.setItem(
        expansionStorageKey(subjectId),
        JSON.stringify(next),
      )
    } catch {
      // Remembering the expansion is a convenience; the map is already showing it.
    }
  }

  return [current.value, update]
}
