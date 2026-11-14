'use client'

import { createContext, FC, ReactNode, useState } from 'react'
import {
  AttentionTileReport,
  OfferedAttentionTile,
  visibleAttentionTiles,
} from './widget-registry'
import { WidgetSettingValues } from './widget-settings'
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
  /** The tiles on offer, muted ones left out, in the order they take slots (see `attentionTiles`). */
  tiles: OfferedAttentionTile[]
  /** Each widget's setting values in effect, by widget id, handed to its tiles. */
  settings?: Record<string, WidgetSettingValues>
  /** Shows the widgets hidden as empty, for a tile whose widget is one. */
  onRevealHiddenWidgets?: () => void
}

/**
 * The row of counts across the top of the home page. A tile at zero gives up
 * its slot to the next, and the strip is hidden when every count is zero.
 */
const AttentionStrip: FC<AttentionStripProps> = ({
  tiles,
  settings,
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

  // A tile that leaves the strip (muted, or its widget switched off) drops its
  // report, so on returning it waits for a fresh count instead of placing
  // slots by the old one.
  const offered = new Set(tiles.map((t) => t.id))
  if (Object.keys(reports).some((id) => !offered.has(id))) {
    setReports((previous) =>
      Object.fromEntries(
        Object.entries(previous).filter(([id]) => offered.has(id)),
      ),
    )
  }

  if (tiles.length === 0) return null

  const visible = visibleAttentionTiles(tiles, reports)

  return (
    <section
      className={styles.attentionStrip}
      aria-label="Needs attention"
      hidden={visible.size === 0}
    >
      {tiles.map(({ id, title, widgetId, Tile }) => (
        <Slot
          key={id}
          id={id}
          visible={visible.has(id)}
          onReport={onReport}
          revealHiddenWidgets={onRevealHiddenWidgets}
        >
          <Tile title={title} settings={settings?.[widgetId]} />
        </Slot>
      ))}
    </section>
  )
}

export default AttentionStrip
