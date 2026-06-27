'use client'

// timeline2/render/item-bar.tsx — a single range bar or milestone diamond.
// Variant-agnostic: it just draws a box. Move/resize/progress handles are
// layered on later (FR-5); this is the static visual + click handling.

import { FC } from 'react'
import type { ItemBox } from '../core/geometry'
import type { DragMode } from '../core/interaction'
import { contrastText } from '../core/color'
import type { ItemRenderProps, TimelineItem } from '../types'
import styles from './timeline.module.css'

export interface ItemBarProps {
  box: ItemBox
  selected: boolean
  editable?: boolean
  /** Px to shift the label right so it stays visible when the bar starts before
   *  the viewport's left edge. */
  labelOffset?: number
  itemRenderer?: FC<ItemRenderProps>
  onClick?: (item: TimelineItem) => void
  /** Begin a move/resize drag (pointer-down on bar body or a handle). */
  onDragStart?: (
    e: React.PointerEvent,
    item: TimelineItem,
    mode: DragMode,
  ) => void
  /** Begin a progress drag (pointer-down on the progress handle). */
  onProgressDragStart?: (e: React.PointerEvent, item: TimelineItem) => void
}

export const ItemBar: FC<ItemBarProps> = ({
  box,
  selected,
  editable,
  labelOffset = 0,
  itemRenderer: Renderer,
  onClick,
  onDragStart,
  onProgressDragStart,
}) => {
  const { item, left, top, width, height } = box

  if (item.kind === 'milestone') {
    // Square rotated 45°; sized to the lane height, centered on its date.
    // Milestones are a single instant — moved as a whole, no resize handles.
    return (
      <div
        data-timeline-item
        className={styles.milestone}
        style={{
          left,
          top,
          width: height,
          height,
          ...(item.color ? { backgroundColor: item.color } : {}),
        }}
        title={item.label}
        onPointerDown={
          editable && onDragStart
            ? (e) => onDragStart(e, item, 'move')
            : undefined
        }
        onClick={() => onClick?.(item)}
      />
    )
  }

  const hasProgress = typeof item.progress === 'number'
  const progressX = hasProgress ? (width * (item.progress ?? 0)) / 100 : 0
  const fontColor = contrastText(item.color)

  return (
    <div
      data-timeline-item
      className={`${styles.bar} ${selected ? styles.barSelected : ''} ${
        editable ? styles.barEditable : ''
      }`}
      style={{
        left,
        top,
        width,
        height,
        ...(item.color ? { backgroundColor: item.color, color: fontColor } : {}),
      }}
      onPointerDown={
        editable && onDragStart
          ? (e) => onDragStart(e, item, 'move')
          : undefined
      }
      onClick={() => onClick?.(item)}
    >
      {/* Progress fill underlay */}
      {hasProgress && (
        <div className={styles.progressFill} style={{ width: progressX }} />
      )}

      <span
        className={styles.barLabel}
        style={{
          ...(labelOffset ? { marginLeft: labelOffset } : undefined),
          // Scale font to fit the bar height so text never clips vertically.
          // Use lineHeight 1.2 to give descenders (g, p, y) room.
          // Cap at 13px (default); floor at 9px for legibility.
          fontSize: Math.max(9, Math.min(Math.floor(height / 1.2), 13)),
          lineHeight: 1.2,
        }}
      >
        {Renderer ? (
          <Renderer
            item={item}
            fontColor={fontColor}
            backgroundColor={item.color ?? 'var(--ant-color-primary)'}
            selected={selected}
          />
        ) : (
          item.label ?? item.id
        )}
      </span>

      {editable && onDragStart && (
        <>
          {/* Resize handles — circles revealed on hover (CSS). */}
          <span
            className={`${styles.handle} ${styles.handleStart}`}
            onPointerDown={(e) => onDragStart(e, item, 'resize-start')}
          />
          <span
            className={`${styles.handle} ${styles.handleEnd}`}
            onPointerDown={(e) => onDragStart(e, item, 'resize-end')}
          />
        </>
      )}

      {editable && hasProgress && onProgressDragStart && (
        <span
          className={styles.progressHandle}
          style={{ left: progressX }}
          onPointerDown={(e) => onProgressDragStart(e, item)}
        />
      )}
    </div>
  )
}
