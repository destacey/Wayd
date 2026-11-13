'use client'

import { createContext, FC, ReactNode, useState } from 'react'
import {
  AttentionTileDefinition,
  AttentionTileReport,
  visibleAttentionTiles,
} from './widget-registry'
import styles from './home.module.css'

/** What a tile in the strip reads: whether it has a slot, and where to report its count. */
export interface AttentionSlot {
  visible: boolean
  report: (report: AttentionTileReport) => void
  /** Shows the widgets hidden as empty, so a tile can scroll to its own. */
  revealHiddenWidgets?: () => void
}

/** Null outside the strip, where a tile always shows. */
export const AttentionSlotContext = createContext<AttentionSlot | null>(null)

const Slot: FC<{
  id: string
  visible: boolean
  onReport: (id: string, report: AttentionTileReport) => void
  revealHiddenWidgets?: () => void
  children: ReactNode
}> = ({ id, visible, onReport, revealHiddenWidgets, children }) => {
  // A report identical to the last is dropped without a render, so a tile
  // re-reporting on every render settles rather than loops.
  const report = (r: AttentionTileReport) => onReport(id, r)
  const slot = { visible, report, revealHiddenWidgets }
  return (
    // A tile without a slot stays mounted, so it still loads and reports its
    // count and can take a slot when the count rises above zero.
    <div className={styles.attentionTile} hidden={!visible}>
      <AttentionSlotContext.Provider value={slot}>
        {children}
      </AttentionSlotContext.Provider>
    </div>
  )
}

export interface AttentionStripProps {
  /** The tiles on offer, in the order they take slots (see `attentionTiles`). */
  tiles: AttentionTileDefinition[]
  /** Shows the widgets hidden as empty, for a tile whose widget is one. */
  onRevealHiddenWidgets?: () => void
}

/**
 * The row of counts across the top of the home page. A tile at zero gives up
 * its slot to the next, and the strip is hidden when every count is zero.
 */
const AttentionStrip: FC<AttentionStripProps> = ({
  tiles,
  onRevealHiddenWidgets,
}) => {
  const [reports, setReports] = useState<
    Record<string, AttentionTileReport | undefined>
  >({})

  const onReport = (id: string, report: AttentionTileReport) => {
    setReports((previous) => {
      const current = previous[id]
      if (
        current?.value === report.value &&
        current?.loading === report.loading
      )
        return previous
      return { ...previous, [id]: report }
    })
  }

  if (tiles.length === 0) return null

  const visible = visibleAttentionTiles(tiles, reports)

  return (
    <section
      className={styles.attentionStrip}
      aria-label="Needs attention"
      hidden={visible.size === 0}
    >
      {tiles.map(({ id, Tile }) => (
        <Slot
          key={id}
          id={id}
          visible={visible.has(id)}
          onReport={onReport}
          revealHiddenWidgets={onRevealHiddenWidgets}
        >
          <Tile />
        </Slot>
      ))}
    </section>
  )
}

export default AttentionStrip
