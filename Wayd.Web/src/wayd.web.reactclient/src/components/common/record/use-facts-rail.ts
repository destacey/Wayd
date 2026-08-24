'use client'

import { Grid } from 'antd'
import { useLocalStorageState } from '@/src/hooks'
import { RecordLayoutConstants } from '@/src/config/theme/theme-constants'

const { useBreakpoint } = Grid

/** One key for every record type: showing the panel is a display preference. */
export const FACTS_RAIL_KEY = 'wayd-record:facts-panel-open'

/** Width is a preference too, and travels with the open state. */
export const FACTS_RAIL_WIDTH_KEY = 'wayd-record:facts-panel-width'

export type FactsRailMode = 'panel' | 'inline' | 'none'

export interface FactsRailState {
  /** How the facts should render at this width. */
  mode: FactsRailMode
  /** Whether the panel is showing. Always true when inline. */
  open: boolean
  setOpen: (open: boolean) => void
  /** True where a toggle belongs — anywhere the facts are behind a panel. */
  showToggle: boolean
  /** Panel width in px, as the user last left it. */
  width: number
  setWidth: (width: number) => void
}

/**
 * Decides how the record facts render at the current width, and remembers
 * whether the panel was left open across records and reloads.
 *
 * The panel overlays the content rather than holding a column beside it, so
 * it costs no width when closed and needs no per-breakpoint default — the
 * only rule is that below `md` the facts move into the content, where they
 * are always visible and there is nothing to toggle.
 */
export const useFactsRail = (hasFacts: boolean): FactsRailState => {
  const screens = useBreakpoint()
  const [open, setOpen] = useLocalStorageState<boolean>(
    FACTS_RAIL_KEY,
    false,
    { version: 1 },
  )
  const [width, setWidth] = useLocalStorageState<number>(
    FACTS_RAIL_WIDTH_KEY,
    RecordLayoutConstants.FACTS_RAIL_WIDTH,
    { version: 1 },
  )

  // A stored width from a wider window (or a hand-edited value) must not be
  // able to squeeze the content out, so the bounds are enforced on read too.
  const boundedWidth = Math.min(
    RecordLayoutConstants.FACTS_RAIL_MAX_WIDTH,
    Math.max(RecordLayoutConstants.FACTS_RAIL_MIN_WIDTH, width),
  )

  if (!hasFacts) {
    return {
      mode: 'none',
      open: false,
      setOpen,
      showToggle: false,
      width: boundedWidth,
      setWidth,
    }
  }

  // Nothing is dropped on mobile, it only moves: the facts render in the
  // content column above the section rather than behind a control.
  if (!screens.md) {
    return {
      mode: 'inline',
      open: true,
      setOpen,
      showToggle: false,
      width: boundedWidth,
      setWidth,
    }
  }

  return {
    mode: 'panel',
    open,
    setOpen,
    showToggle: true,
    width: boundedWidth,
    setWidth,
  }
}
