'use client'

// use-gantt-visibility.ts — remembers whether a Gantt pane is shown.
//
// The toggle is a display preference, not a property of the record being
// viewed: a user who hides the chart on one roadmap almost certainly wants it
// hidden on every roadmap. So the preference is stored per AREA (one key for
// all roadmaps, one for all project plans) rather than per record — the same
// reasoning as the record facts rail's single key across record types.
//
// Kept beside the engine so both consumers share one default rather than each
// hand-rolling localStorage wiring.

import { useCallback } from 'react'
import { useLocalStorageState } from '@/src/hooks'

/**
 * One key per area that hosts a Gantt pane. Adding a pane elsewhere means
 * adding a key here, so the areas stay enumerable rather than letting
 * free-form strings fragment the preference.
 */
export const GANTT_VISIBILITY_KEYS = {
  roadmap: 'wayd-gantt:roadmap-chart-visible',
  'project-plan': 'wayd-gantt:project-plan-chart-visible',
} as const

export type GanttArea = keyof typeof GANTT_VISIBILITY_KEYS

export interface GanttVisibilityState {
  /** Whether the chart pane is currently shown. */
  visible: boolean
  /** Flip the preference; persisted immediately. */
  toggle: () => void
}

/**
 * Show/hide state for an area's Gantt pane, remembered across records and
 * reloads.
 *
 * Defaults to shown, so a user who has never touched the toggle still gets the
 * chart — the behavior before this was persisted.
 */
export const useGanttVisibility = (area: GanttArea): GanttVisibilityState => {
  const [visible, setVisible] = useLocalStorageState<boolean>(
    GANTT_VISIBILITY_KEYS[area],
    true,
    { version: 1 },
  )

  // Memoized because it's handed to the toolbar as a prop.
  const toggle = useCallback(() => setVisible((v) => !v), [setVisible])

  return { visible, toggle }
}
