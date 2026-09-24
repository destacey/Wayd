'use client'

import { useState } from 'react'
import type { DependencyFarSide } from './dependency-neighbourhood'

/** What the reader opened on one record's map: which records are expanded, and which counts revealed. */
export interface DependencyMapExpansionState {
  left: Record<string, { showAll?: boolean }>
  right: Record<string, { showAll?: boolean }>
  /** Sides where the subject's own overflow has been revealed. */
  subjectShowAll: DependencyFarSide[]
}

export const emptyExpansionState = (): DependencyMapExpansionState => ({
  left: {},
  right: {},
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
      left: parsed.left ?? {},
      right: parsed.right ?? {},
      subjectShowAll: parsed.subjectShowAll ?? [],
    }
  } catch {
    return emptyExpansionState()
  }
}

/**
 * The expansions open on a record's map, kept for the browser tab and per record.
 *
 * Per record, because an expansion is a path out from one subject and means nothing centred on
 * another: moving to a different record starts that map clean, and coming back finds this one as it
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

  // Re-read in render rather than in an effect when the page moves to another record, so the new map
  // never draws a frame with the old record's expansions.
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

/**
 * Expands a record, or collapses it when it already is.
 *
 * Collapsing takes everything the expansion brought in with it, nested expansions included, so expanding
 * the record again starts from one column rather than restoring a stale path. `placedAfter` draws the map
 * from a candidate state and reports what it placed; pass null while anything is still loading, because a
 * record not yet reached is not the same as one no longer reachable.
 */
export const toggleExpansion = (
  state: DependencyMapExpansionState,
  side: DependencyFarSide,
  recordId: string,
  placedAfter:
    | ((
        next: DependencyMapExpansionState,
      ) => Record<DependencyFarSide, string[]>)
    | null,
): DependencyMapExpansionState => {
  const next: DependencyMapExpansionState = {
    left: { ...state.left },
    right: { ...state.right },
    subjectShowAll: state.subjectShowAll,
  }

  if (!next[side][recordId]) {
    next[side][recordId] = {}
    return next
  }

  delete next[side][recordId]

  if (placedAfter) {
    const placed = placedAfter(next)
    for (const s of ['left', 'right'] as const) {
      for (const id of Object.keys(next[s])) {
        if (!placed[s].includes(id)) delete next[s][id]
      }
    }
  }

  return next
}

/** Draws the records a count left off: an expansion's, or the subject's own when `ownerId` is null. */
export const revealOverflow = (
  state: DependencyMapExpansionState,
  side: DependencyFarSide,
  ownerId: string | null,
): DependencyMapExpansionState =>
  ownerId === null
    ? {
        ...state,
        subjectShowAll: [...new Set([...state.subjectShowAll, side])],
      }
    : { ...state, [side]: { ...state[side], [ownerId]: { showAll: true } } }
