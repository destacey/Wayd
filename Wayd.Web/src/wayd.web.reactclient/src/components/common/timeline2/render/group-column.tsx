'use client'

// timeline2/render/group-column.tsx — the left label column. Renders one cell
// per resolved row, aligned to the row's top/height, indented by depth. Scrolls
// vertically in sync with the chart body (the parent keeps them aligned).
//
// Labels wrap; a wrapped label can be taller than the lane-based row height, so
// each label's natural height is measured and reported up (onMeasure) — the
// parent grows the affected rows to fit.

import { FC, useEffect, useRef } from 'react'
import type { ResolvedRow, TimelineGroup } from '../core/types'
import type { GroupRenderProps } from '../types'
import styles from './timeline.module.css'

export interface GroupColumnProps {
  rows: ResolvedRow[]
  totalHeight: number
  width: number | string
  /** Lookup for a row's group, by groupId. */
  groupsById: Map<string, TimelineGroup>
  groupRenderer?: FC<GroupRenderProps>
  /** Reports measured label heights (incl. cell padding) keyed by groupId. */
  onMeasure?: (heights: Map<string, number>) => void
}

const INDENT_PER_DEPTH = 14
// Vertical padding applied by .groupCell (top + bottom), kept in sync with CSS.
const CELL_VPAD = 8

export const GroupColumn: FC<GroupColumnProps> = ({
  rows,
  totalHeight,
  width,
  groupsById,
  groupRenderer: Renderer,
  onMeasure,
}) => {
  const containerRef = useRef<HTMLDivElement>(null)

  // Measure each label's natural height after layout (and on column resize) and
  // report the per-group cell height so the parent can grow rows to fit.
  useEffect(() => {
    const el = containerRef.current
    if (!el || !onMeasure) return
    const measure = () => {
      const heights = new Map<string, number>()
      el.querySelectorAll<HTMLElement>('[data-row-key]').forEach((label) => {
        const id = label.dataset.rowKey
        if (id) heights.set(id, Math.ceil(label.scrollHeight) + CELL_VPAD)
      })
      onMeasure(heights)
    }
    measure()
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [rows, onMeasure])

  return (
    <div
      ref={containerRef}
      className={styles.groupColumn}
      style={{ width, height: totalHeight }}
    >
      {rows.map((row, rowIndex) => {
        const group = row.groupId ? groupsById.get(row.groupId) : undefined
        const label = group?.label ?? row.groupId ?? ''
        return (
          <div
            key={row.rowKey ?? row.groupId ?? `grow-${rowIndex}`}
            className={`${styles.groupCell} ${
              rowIndex % 2 === 1 ? styles.rowAlt : ''
            }`}
            style={{ top: row.top, height: row.height }}
            title={label}
          >
            <span
              className={styles.groupLabel}
              data-row-key={row.rowKey ?? row.groupId}
              style={{ paddingLeft: row.depth * INDENT_PER_DEPTH }}
            >
              {Renderer && group ? (
                <Renderer
                  group={group}
                  depth={row.depth}
                  collapsed={group.collapsed === true}
                />
              ) : (
                label
              )}
            </span>
          </div>
        )
      })}
    </div>
  )
}
